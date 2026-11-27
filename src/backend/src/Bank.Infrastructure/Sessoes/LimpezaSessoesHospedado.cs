using Bank.Application.Autenticacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Sessoes;

/// <summary>
/// Agendamento da limpeza das sessões encerradas no Worker (specs/016-sessao-refresh-token, FR-013; research R8).
/// Chama <see cref="LimparSessoes"/> na partida e a cada intervalo; uma falha é registrada e não encerra o serviço.
/// </summary>
public sealed class LimpezaSessoesOptions
{
    public const string Secao = "LimpezaSessoes";

    public bool Habilitada { get; set; } = true;

    public int IntervaloMinutos { get; set; } = 60;
}

internal sealed class ValidadorLimpezaSessoesOptions : IValidateOptions<LimpezaSessoesOptions>
{
    public ValidateOptionsResult Validate(string? name, LimpezaSessoesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.IntervaloMinutos < 1
            ? ValidateOptionsResult.Fail($"{LimpezaSessoesOptions.Secao}:{nameof(LimpezaSessoesOptions.IntervaloMinutos)} deve ser de pelo menos 1 minuto.")
            : ValidateOptionsResult.Success;
    }
}

internal sealed partial class LimpezaSessoesHospedado(
    IServiceScopeFactory scopeFactory,
    IOptions<LimpezaSessoesOptions> opcoes,
    ILogger<LimpezaSessoesHospedado> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opcoes.Value.Habilitada)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var escopo = scopeFactory.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<LimparSessoes>().ExecutarAsync(stoppingToken);
            }
            catch (Exception excecao) when (!stoppingToken.IsCancellationRequested)
            {
                LogFalha(logger, excecao);
            }

            await Task.Delay(TimeSpan.FromMinutes(opcoes.Value.IntervaloMinutos), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao limpar as sessões encerradas; nova tentativa na próxima rodada")]
    private static partial void LogFalha(ILogger logger, Exception excecao);
}
