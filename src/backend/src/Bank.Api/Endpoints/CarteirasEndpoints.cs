using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Carteiras;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bank.Api.Endpoints;

/// <summary>
/// Consulta da própria carteira (US3; contracts/usuarios-carteiras-api.md). O usuário vem do
/// token (transporte); o caso de uso busca sempre o saldo atual.
/// </summary>
public static class CarteirasEndpoints
{
    public const string Grupo = "/api/v1/carteiras";
    public const string RotaMinha = Grupo + "/minha";

    public static IEndpointRouteBuilder MapCarteirasEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup(Grupo)
            .WithTags("Carteiras")
            .MapGet("/minha", ObterMinhaAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ObterMinhaCarteira")
            .WithSummary("Retorna a carteira e o saldo atual do usuário autenticado.")
            .Produces<MinhaCarteiraResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ObterMinhaAsync(ClaimsPrincipal usuario, ObterMinhaCarteira casoDeUso, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
        if (!resultado.Sucesso)
        {
            return resultado.ParaProblema();
        }

        var carteira = resultado.Valor;
        return TypedResults.Ok(new MinhaCarteiraResposta(
            carteira.CarteiraId,
            carteira.UsuarioId,
            carteira.NomeTitular,
            carteira.TipoConta.ToString(),
            carteira.Saldo,
            carteira.Moeda,
            carteira.AtualizadoEm));
    }

    public sealed record MinhaCarteiraResposta(
        Guid CarteiraId,
        Guid UsuarioId,
        string NomeTitular,
        string TipoConta,
        decimal Saldo,
        string Moeda,
        DateTimeOffset AtualizadoEm);
}
