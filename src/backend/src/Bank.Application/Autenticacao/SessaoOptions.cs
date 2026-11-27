using Bank.Domain.Sessoes;
using Microsoft.Extensions.Options;

namespace Bank.Application.Autenticacao;

/// <summary>
/// Prazos da sessão (specs/016-sessao-refresh-token, FR-002, FR-008, FR-013; contracts/autenticacao-sessao-api.md,
/// "Configuração"). Os padrões são os de produção; o e2e usa prazos curtos.
/// </summary>
public sealed class SessaoOptions
{
    public const string Secao = "Sessao";

    public int InatividadeSegundos { get; set; } = 900;

    public int LimiteAbsolutoHoras { get; set; } = 8;

    public int ToleranciaReusoSegundos { get; set; } = 30;

    public int RetencaoDias { get; set; } = 7;

    public LimitesSessao Limites() => new(
        TimeSpan.FromSeconds(InatividadeSegundos),
        TimeSpan.FromHours(LimiteAbsolutoHoras),
        TimeSpan.FromSeconds(ToleranciaReusoSegundos));

    public TimeSpan Retencao => TimeSpan.FromDays(RetencaoDias);
}

/// <summary>Valida na inicialização: prazos positivos, inatividade ≤ limite absoluto e tolerância &lt; inatividade.</summary>
public sealed class ValidadorSessaoOptions : IValidateOptions<SessaoOptions>
{
    public ValidateOptionsResult Validate(string? name, SessaoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();

        if (options.InatividadeSegundos <= 0)
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.InatividadeSegundos)} deve ser maior que zero.");
        }

        if (options.LimiteAbsolutoHoras <= 0)
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.LimiteAbsolutoHoras)} deve ser maior que zero.");
        }

        if (options.ToleranciaReusoSegundos <= 0)
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.ToleranciaReusoSegundos)} deve ser maior que zero.");
        }

        if (options.RetencaoDias <= 0)
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.RetencaoDias)} deve ser maior que zero.");
        }

        if (options.InatividadeSegundos > 0 && options.LimiteAbsolutoHoras > 0
            && TimeSpan.FromSeconds(options.InatividadeSegundos) > TimeSpan.FromHours(options.LimiteAbsolutoHoras))
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.InatividadeSegundos)} não pode passar de {SessaoOptions.Secao}:{nameof(SessaoOptions.LimiteAbsolutoHoras)}.");
        }

        if (options.ToleranciaReusoSegundos > 0 && options.InatividadeSegundos > 0
            && options.ToleranciaReusoSegundos >= options.InatividadeSegundos)
        {
            falhas.Add($"{SessaoOptions.Secao}:{nameof(SessaoOptions.ToleranciaReusoSegundos)} deve ser menor que {SessaoOptions.Secao}:{nameof(SessaoOptions.InatividadeSegundos)}.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
