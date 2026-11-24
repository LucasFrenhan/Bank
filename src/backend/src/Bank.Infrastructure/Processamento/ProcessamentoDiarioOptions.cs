using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Processamento;

/// <summary>Agendamento do processamento diário no Worker (specs/014-cartao-credito-cobrancas, research R7).</summary>
public sealed class ProcessamentoDiarioOptions
{
    public const string Secao = "ProcessamentoDiario";

    public bool Habilitado { get; set; } = true;

    /// <summary>Intervalo entre verificações; a 00:00 de Brasília sempre dispara uma.</summary>
    public int IntervaloSegundos { get; set; } = 60;
}

internal sealed class ValidadorProcessamentoDiarioOptions : IValidateOptions<ProcessamentoDiarioOptions>
{
    public ValidateOptionsResult Validate(string? name, ProcessamentoDiarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.IntervaloSegundos < 1
            ? ValidateOptionsResult.Fail($"{ProcessamentoDiarioOptions.Secao}:{nameof(ProcessamentoDiarioOptions.IntervaloSegundos)} deve ser de pelo menos 1 segundo.")
            : ValidateOptionsResult.Success;
    }
}
