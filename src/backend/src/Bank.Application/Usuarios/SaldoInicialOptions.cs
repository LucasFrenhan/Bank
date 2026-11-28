using Bank.SharedKernel;
using Microsoft.Extensions.Options;

namespace Bank.Application.Usuarios;

/// <summary>
/// Saldo inicial de demonstração por perfil (RN-05, FR-013; research R8). Fora de
/// <c>Production</c>: PF 10.00 e PJ 0.00 por configuração; em <c>Production</c> ambos 0.00.
/// </summary>
public sealed class SaldoInicialOptions
{
    public const string Secao = "Carteiras:SaldoInicial";

    public decimal PessoaFisica { get; set; }

    public decimal PessoaJuridica { get; set; }
}

/// <summary>
/// Valida a configuração na inicialização: valores ≥ 0, até 2 casas decimais e zero em
/// <c>Production</c> (dados de demonstração só existem fora de produção).
/// </summary>
public sealed class ValidadorSaldoInicialOptions(bool ambienteDeProducao) : IValidateOptions<SaldoInicialOptions>
{
    public ValidateOptionsResult Validate(string? name, SaldoInicialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();
        Validar(nameof(SaldoInicialOptions.PessoaFisica), options.PessoaFisica, falhas);
        Validar(nameof(SaldoInicialOptions.PessoaJuridica), options.PessoaJuridica, falhas);

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private void Validar(string perfil, decimal valor, List<string> falhas)
    {
        var chave = $"{SaldoInicialOptions.Secao}:{perfil}";

        if (valor < 0)
        {
            falhas.Add($"{chave} não pode ser negativo.");
        }

        if (!Dinheiro.TemAteDuasCasas(valor))
        {
            falhas.Add($"{chave} deve ter no máximo 2 casas decimais.");
        }

        if (ambienteDeProducao && valor != 0)
        {
            falhas.Add($"{chave} deve ser 0.00 em Production: saldo de demonstração só existe fora de produção.");
        }
    }
}
