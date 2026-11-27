using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bank.Infrastructure.Seguranca;

/// <summary>
/// Configuração do JWT (research R4). <see cref="ChaveAssinatura"/> é segredo: user-secrets
/// (SDK), <c>JWT_CHAVE_ASSINATURA</c> (Compose) ou aleatória (testes); nunca versionada.
/// </summary>
public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChaveBytes = 32;
    public const string Algoritmo = SecurityAlgorithms.HmacSha256;

    public string Emissor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    public int ExpiracaoSegundos { get; set; } = 300;

    public string ChaveAssinatura { get; set; } = string.Empty;

    public SymmetricSecurityKey CriarChave() => new(Encoding.UTF8.GetBytes(ChaveAssinatura));

    /// <summary>Parâmetros de validação usados pelo JwtBearer da API.</summary>
    public TokenValidationParameters CriarParametrosValidacao() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Emissor,
        ValidateAudience = true,
        ValidAudience = Audiencia,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CriarChave(),
        ValidAlgorithms = [Algoritmo],
        ClockSkew = TimeSpan.Zero,
    };
}

/// <summary>Valida a configuração do JWT na inicialização; a mensagem identifica a chave.</summary>
internal sealed class ValidadorJwtOptions : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Emissor))
        {
            falhas.Add($"{JwtOptions.Secao}:{nameof(JwtOptions.Emissor)} é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(options.Audiencia))
        {
            falhas.Add($"{JwtOptions.Secao}:{nameof(JwtOptions.Audiencia)} é obrigatório.");
        }

        if (options.ExpiracaoSegundos <= 0)
        {
            falhas.Add($"{JwtOptions.Secao}:{nameof(JwtOptions.ExpiracaoSegundos)} deve ser maior que zero.");
        }

        if (Encoding.UTF8.GetByteCount(options.ChaveAssinatura ?? string.Empty) < JwtOptions.TamanhoMinimoChaveBytes)
        {
            falhas.Add($"{JwtOptions.Secao}:{nameof(JwtOptions.ChaveAssinatura)} é obrigatória e deve ter ao menos {JwtOptions.TamanhoMinimoChaveBytes} bytes (use dotnet user-secrets ou a variável JWT_CHAVE_ASSINATURA).");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
