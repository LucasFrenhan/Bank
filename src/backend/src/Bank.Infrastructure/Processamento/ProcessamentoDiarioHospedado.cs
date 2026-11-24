using Bank.Application.Processamento;
using Bank.Application.Transferencias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Processamento;

/// <summary>
/// Chama <see cref="ProcessarDia"/> na partida, à 00:00 de Brasília e a cada intervalo
/// (specs/014-cartao-credito-cobrancas, FR-022; research R7). Uma falha é registrada e não encerra o serviço.
/// </summary>
internal sealed partial class ProcessamentoDiarioHospedado(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessamentoDiarioOptions> opcoes,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    ILogger<ProcessamentoDiarioHospedado> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.Habilitado)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var escopo = scopeFactory.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ProcessarDia>().ExecutarAsync(stoppingToken);
            }
            catch (Exception excecao) when (!stoppingToken.IsCancellationRequested)
            {
                LogFalha(logger, excecao);
            }

            await Task.Delay(EsperaAteAProximaRodada(relogio.GetUtcNow()), stoppingToken);
        }
    }

    /// <summary>O intervalo configurado ou, se for antes, até a próxima 00:00 de Brasília.</summary>
    internal TimeSpan EsperaAteAProximaRodada(DateTimeOffset agora)
    {
        var intervalo = TimeSpan.FromSeconds(opcoes.Value.IntervaloSegundos);
        var ateAMeiaNoite = calendario.Hoje(agora).FimUtc - agora;
        return ateAMeiaNoite < intervalo ? ateAMeiaNoite : intervalo;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao executar o processamento diário; nova tentativa na próxima rodada")]
    private static partial void LogFalha(ILogger logger, Exception excecao);
}
