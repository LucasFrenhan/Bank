using Bank.Application.Abstracoes;
using Bank.Domain.Transferencias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Consumidor <c>notificacoes-transferencias</c> da fila <c>notificacoes.transferencias.fila</c>
/// (specs/010-mensageria-rabbitmq, US1, FR-016): lê <c>transferencia.concluida</c> v1 e chama
/// <see cref="INotificadorTransferencia"/>.
/// </summary>
internal sealed class ConsumidorNotificacoesTransferencia(
    IServiceScopeFactory scopeFactory,
    ConexaoRabbitMq conexao,
    IOptions<MensageriaOptions> opcoes,
    TelemetriaMensageria telemetria,
    EstadoMensageria estado,
    TimeProvider relogio,
    ILogger<ConsumidorNotificacoesTransferencia> logger)
    : ConsumidorEventos<NotificacaoTransferencia>(scopeFactory, conexao, opcoes, telemetria, estado, relogio, logger)
{
    protected override DescritorFila Fila => TopologiaMensageria.Transferencias;

    protected override string TipoEvento => TransferenciaConcluida.Tipo;

    protected override LeituraMensagem<NotificacaoTransferencia> Ler(ReadOnlyMemory<byte> corpo, IReadOnlyBasicProperties propriedades) =>
        LeitorTransferenciaConcluida.Ler(corpo, propriedades);

    protected override Task ProcessarAsync(IServiceProvider servicos, NotificacaoTransferencia mensagem, CancellationToken cancellationToken) =>
        servicos.GetRequiredService<INotificadorTransferencia>().NotificarAsync(mensagem, cancellationToken);
}
