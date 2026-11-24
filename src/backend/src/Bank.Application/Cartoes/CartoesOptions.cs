using Bank.Domain.Comum;
using Microsoft.Extensions.Options;

namespace Bank.Application.Cartoes;

/// <summary>Limite dos cartões novos e juros por atraso (specs/014-cartao-credito-cobrancas, FR-005, FR-028a).</summary>
public sealed class CartoesOptions
{
    public const string Secao = "Cartoes";

    public decimal LimitePadrao { get; set; } = 5000.00m;

    public decimal JurosAtrasoMensalPercentual { get; set; } = 15.00m;
}

/// <summary>Valida na inicialização: limite &gt; 0 e juros entre 0 e 100, ambos com até 2 casas.</summary>
public sealed class ValidadorCartoesOptions : IValidateOptions<CartoesOptions>
{
    public ValidateOptionsResult Validate(string? name, CartoesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();

        if (options.LimitePadrao <= 0 || !Dinheiro.TemAteDuasCasas(options.LimitePadrao))
        {
            falhas.Add($"{CartoesOptions.Secao}:{nameof(CartoesOptions.LimitePadrao)} deve ser maior que zero e ter no máximo 2 casas decimais.");
        }

        if (options.JurosAtrasoMensalPercentual is < 0 or > 100 || !Dinheiro.TemAteDuasCasas(options.JurosAtrasoMensalPercentual))
        {
            falhas.Add($"{CartoesOptions.Secao}:{nameof(CartoesOptions.JurosAtrasoMensalPercentual)} deve estar entre 0 e 100 e ter no máximo 2 casas decimais.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
