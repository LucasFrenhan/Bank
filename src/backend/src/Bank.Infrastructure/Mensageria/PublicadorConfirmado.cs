using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Bank.Infrastructure.Mensageria;

/// <summary>Desfecho de uma publicação com confirmação do broker.</summary>
internal enum ResultadoPublicacao
{
    Confirmado,
    SemRota,
    Nack,
    Timeout,
    CanalFechado,
}

/// <summary>
/// Publicação com publisher confirm e resultado tipado (specs/010-mensageria-rabbitmq, research R4),
/// para que o despachante e o consumidor sejam testáveis sem broker.
/// </summary>
internal interface IPublicadorConfirmado
{
    Task<ResultadoPublicacao> PublicarAsync(
        string exchange,
        string routingKey,
        BasicProperties propriedades,
        ReadOnlyMemory<byte> corpo,
        CancellationToken cancellationToken);
}

/// <summary>
/// Implementação sobre um canal com confirmações rastreadas: o <c>BasicPublishAsync</c> só retorna
/// depois do ack; nack e devolução sem rota (<c>mandatory</c>) lançam <see cref="PublishException"/>.
/// </summary>
internal sealed class PublicadorConfirmado(IChannel canal, TimeSpan timeout) : IPublicadorConfirmado
{
    public async Task<ResultadoPublicacao> PublicarAsync(
        string exchange,
        string routingKey,
        BasicProperties propriedades,
        ReadOnlyMemory<byte> corpo,
        CancellationToken cancellationToken)
    {
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(timeout);
        try
        {
            await canal.BasicPublishAsync(exchange, routingKey, mandatory: true, propriedades, corpo, prazo.Token);
            return ResultadoPublicacao.Confirmado;
        }
        catch (PublishReturnException)
        {
            return ResultadoPublicacao.SemRota;
        }
        catch (PublishException)
        {
            return ResultadoPublicacao.Nack;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoPublicacao.Timeout;
        }
        catch (Exception excecao) when (excecao is AlreadyClosedException or OperationInterruptedException or ObjectDisposedException)
        {
            return ResultadoPublicacao.CanalFechado;
        }
    }
}
