using Bank.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Bank.Api.Seguranca;

/// <summary>
/// Validação do JWT Bearer e políticas de autorização (FR-020, FR-023; research R4, R5).
/// Desafios 401/403 saem sem corpo e são convertidos em <c>ProblemDetails</c> pelo pipeline de
/// erros (specs/002-setup-api, R6).
/// </summary>
public static class AutenticacaoExtensions
{
    /// <summary>Token válido com as claims de identidade exigidas (<c>sub</c>, <c>tipo_conta</c>, <c>carteira_id</c>).</summary>
    public const string PoliticaUsuarioAutenticado = "UsuarioAutenticado";

    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false; // mantém sub, tipo_conta e carteira_id como emitidos
                bearer.TokenValidationParameters = jwt.Value.CriarParametrosValidacao();
            });

        var autorizacao = services.AddAuthorizationBuilder()
            .AddPolicy(PoliticaUsuarioAutenticado, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimsToken.Sub)
                .RequireClaim(ClaimsToken.TipoConta)
                .RequireClaim(ClaimsToken.CarteiraId));

        PoliticasAutorizacao.AdicionarPoliticasDePermissao(autorizacao);

        return services;
    }
}
