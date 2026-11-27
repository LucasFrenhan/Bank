using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Autenticacao;

/// <summary>
/// Remove as sessões encerradas (revogadas ou vencidas) há mais que a retenção, com as credenciais
/// (specs/016-sessao-refresh-token, FR-013; research R8). Só registra log e métrica quando remove algo.
/// </summary>
public sealed partial class LimparSessoes(
    ILimpezaSessoes limpeza,
    IOptionsMonitor<SessaoOptions> opcoes,
    TimeProvider relogio,
    MetricasUsuarios metricas,
    ILogger<LimparSessoes> logger)
{
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var configuracao = opcoes.CurrentValue;
        var limite = relogio.GetUtcNow() - configuracao.Retencao;
        var removidas = await limpeza.RemoverEncerradasAntesDeAsync(
            limite, TimeSpan.FromSeconds(configuracao.InatividadeSegundos), cancellationToken);

        if (removidas > 0)
        {
            metricas.RegistrarRemocao(removidas);
            LogRemovidas(logger, removidas);
        }

        return removidas;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Quantidade} sessões encerradas removidas")]
    private static partial void LogRemovidas(ILogger logger, int quantidade);
}
