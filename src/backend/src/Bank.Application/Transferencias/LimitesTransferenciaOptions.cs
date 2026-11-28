using Bank.Domain.Transferencias;
using Bank.SharedKernel;
using Microsoft.Extensions.Options;

namespace Bank.Application.Transferencias;

/// <summary>Limites de um perfil; <c>null</c> (ausente ou vazio na configuração) = sem limite.</summary>
public sealed class LimitesPerfilOptions
{
    public decimal? PorOperacao { get; set; }

    public decimal? Diario { get; set; }
}

/// <summary>
/// Limites de transferência por perfil (specs/009-limite-transferencia, FR-005, FR-006; research
/// R2). Padrões no <c>appsettings.json</c>; valor vazio sobrescreve o padrão e desliga o limite.
/// Só Pessoa Física transfere (spec 006).
/// </summary>
public sealed class LimitesTransferenciaOptions
{
    public const string Secao = "Transferencias:Limites";

    public LimitesPerfilOptions PessoaFisica { get; set; } = new();

    public LimitesTransferencia ParaPessoaFisica() => new(PessoaFisica.PorOperacao, PessoaFisica.Diario);
}

/// <summary>Valida na inicialização: limites &gt; 0, até 2 casas e por operação ≤ diário.</summary>
public sealed class ValidadorLimitesTransferenciaOptions : IValidateOptions<LimitesTransferenciaOptions>
{
    public ValidateOptionsResult Validate(string? name, LimitesTransferenciaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();
        var perfil = options.PessoaFisica ?? new LimitesPerfilOptions();
        var prefixo = $"{LimitesTransferenciaOptions.Secao}:{nameof(LimitesTransferenciaOptions.PessoaFisica)}";

        Validar($"{prefixo}:{nameof(LimitesPerfilOptions.PorOperacao)}", perfil.PorOperacao, falhas);
        Validar($"{prefixo}:{nameof(LimitesPerfilOptions.Diario)}", perfil.Diario, falhas);

        if (perfil.PorOperacao is { } porOperacao && perfil.Diario is { } diario && porOperacao > diario)
        {
            falhas.Add($"{prefixo}:{nameof(LimitesPerfilOptions.Diario)} não pode ser menor que o limite por operação.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void Validar(string chave, decimal? valor, List<string> falhas)
    {
        if (valor is not { } limite)
        {
            return;
        }

        if (limite <= 0)
        {
            falhas.Add($"{chave} deve ser maior que zero (ou vazio, para não ter limite).");
        }

        if (!Dinheiro.TemAteDuasCasas(limite))
        {
            falhas.Add($"{chave} deve ter no máximo 2 casas decimais.");
        }
    }
}
