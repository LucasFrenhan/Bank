using System.Text;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Seguranca;

/// <summary>
/// Chave secreta que deriva número e CVV dos cartões (specs/014-cartao-credito-cobrancas, research R1):
/// user-secrets (SDK), <c>CARTOES_CHAVE_DERIVACAO</c> (Compose) ou aleatória (testes); nunca versionada.
/// Trocá-la muda os dados de todos os cartões.
/// </summary>
public sealed class ChaveCartoesOptions
{
    public const string Secao = "Cartoes";
    public const int TamanhoMinimoChaveBytes = 32;

    public string ChaveDerivacao { get; set; } = string.Empty;
}

/// <summary>Valida a chave na inicialização; a mensagem identifica a configuração.</summary>
internal sealed class ValidadorChaveCartoesOptions : IValidateOptions<ChaveCartoesOptions>
{
    public ValidateOptionsResult Validate(string? name, ChaveCartoesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Encoding.UTF8.GetByteCount(options.ChaveDerivacao ?? string.Empty) < ChaveCartoesOptions.TamanhoMinimoChaveBytes
            ? ValidateOptionsResult.Fail($"{ChaveCartoesOptions.Secao}:{nameof(ChaveCartoesOptions.ChaveDerivacao)} é obrigatória e deve ter ao menos {ChaveCartoesOptions.TamanhoMinimoChaveBytes} bytes (use dotnet user-secrets ou a variável CARTOES_CHAVE_DERIVACAO).")
            : ValidateOptionsResult.Success;
    }
}
