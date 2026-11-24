using Bank.Application.Abstracoes;
using Bank.Domain.Cobrancas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Consumidor <c>notificacoes-cobrancas</c> da fila <c>notificacoes.cobrancas.fila</c>
/// (specs/014-cartao-credito-cobrancas, US7): lê <c>cobranca.paga</c> v1 e chama <see cref="INotificadorCobranca"/>.
/// </summary>
internal sealed class ConsumidorNotificacoesCobranca(
    IServiceScopeFactory scopeFactory,
    ConexaoRabbitMq conexao,
    IOptions<MensageriaOptions> opcoes,
    TelemetriaMensageria telemetria,
    EstadoMensageria estado,
    TimeProvider relogio,
    ILogger<ConsumidorNotificacoesCobranca> logger)
    : ConsumidorEventos<NotificacaoCobranca>(scopeFactory, conexao, opcoes, telemetria, estado, relogio, logger)
{
    protected override DescritorFila Fila => TopologiaMensageria.Cobrancas;

    protected override string TipoEvento => CobrancaPaga.Tipo;

    protected override LeituraMensagem<NotificacaoCobranca> Ler(ReadOnlyMemory<byte> corpo, IReadOnlyBasicProperties propriedades) =>
        LeitorCobrancaPaga.Ler(corpo, propriedades);

    protected override Task ProcessarAsync(IServiceProvider servicos, NotificacaoCobranca mensagem, CancellationToken cancellationToken) =>
        servicos.GetRequiredService<INotificadorCobranca>().NotificarAsync(mensagem, cancellationToken);
}
