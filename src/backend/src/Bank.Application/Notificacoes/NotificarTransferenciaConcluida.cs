using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Notificacoes;

/// <summary>
/// Notifica pagador e recebedor de uma transferência concluída (specs/010-mensageria-rabbitmq,
/// FR-016, research R8). Não há envio real nesta spec: a notificação é um log estruturado por
/// destinatário, sem nome nem descrição (FR-023). O envio real (e-mail, push) entra aqui.
/// </summary>
public sealed partial class NotificarTransferenciaConcluida(
    MetricasNotificacoes metricas,
    ILogger<NotificarTransferenciaConcluida> logger) : INotificadorTransferencia
{
    public const string PapelPagador = "pagador";
    public const string PapelRecebedor = "recebedor";

    public Task NotificarAsync(NotificacaoTransferencia notificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacao);

        LogNotificacao(logger, PapelPagador, notificacao.PagadorUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.TransferenciaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelPagador);

        LogNotificacao(logger, PapelRecebedor, notificacao.RecebedorUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.TransferenciaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelRecebedor);

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 5100, EventName = "NotificacaoTransferencia", Level = LogLevel.Information,
        Message = "Notificação de transferência para o {Papel} {UsuarioId}: evento {EventoId}, transferência {TransferenciaId}, valor {Valor}")]
    private static partial void LogNotificacao(ILogger logger, string papel, Guid usuarioId, Guid eventoId, Guid transferenciaId, decimal valor);
}
