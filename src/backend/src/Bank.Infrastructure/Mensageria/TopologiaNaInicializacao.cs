using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Declara a topologia assim que o broker responde (specs/010-mensageria-rabbitmq, FR-007). Não
/// bloqueia a partida: com o broker fora, tenta de novo a cada 2 s até conseguir. Despachante e
/// consumidores também declaram a topologia em cada canal que abrem.
/// </summary>
internal sealed partial class TopologiaNaInicializacao(
    ConexaoRabbitMq conexao,
    IOptions<MensageriaOptions> opcoes,
    ILogger<TopologiaNaInicializacao> logger) : BackgroundService
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conexaoAberta = await conexao.ObterAsync(stoppingToken);
                await using var canal = await conexaoAberta.CreateChannelAsync(cancellationToken: stoppingToken);
                await TopologiaMensageria.DeclararAsync(canal, opcoes.Value.IntervalosNovaTentativaMs, stoppingToken);
                LogTopologiaDeclarada(logger);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Broker ainda fora: tenta de novo; o processo não pode cair.
            catch (Exception excecao)
#pragma warning restore CA1031
            {
                LogFalhaTopologia(logger, excecao.GetType().Name, excecao.Message);
            }

            try
            {
                await Task.Delay(Espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(EventId = 5010, EventName = "TopologiaDeclarada", Level = LogLevel.Information,
        Message = "Topologia de mensageria declarada")]
    private static partial void LogTopologiaDeclarada(ILogger logger);

    [LoggerMessage(EventId = 5011, EventName = "FalhaTopologia", Level = LogLevel.Warning,
        Message = "Falha ao declarar a topologia de mensageria ({Tipo}): {Motivo}")]
    private static partial void LogFalhaTopologia(ILogger logger, string tipo, string motivo);
}
