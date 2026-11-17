using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Usuarios;

namespace Bank.Api.Endpoints;

/// <summary>
/// Dados do titular autenticado (specs/005-usuarios-me; contracts/usuarios-me-api.md). O titular
/// vem só do token; a resposta não pode ser guardada em cache (dados pessoais).
/// </summary>
public static class UsuariosEndpoints
{
    public const string Grupo = "/api/v1/usuarios";

    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup(Grupo)
            .WithTags("Usuários")
            .MapGet("/me", ObterAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ObterTitular")
            .WithSummary("Retorna os dados cadastrais do usuário autenticado.")
            .Produces<TitularResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ObterAsync(
        ClaimsPrincipal usuario,
        ObterTitular casoDeUso,
        HttpResponse resposta,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
        if (!resultado.Sucesso)
        {
            return resultado.ParaProblema();
        }

        var titular = resultado.Valor;
        resposta.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new TitularResposta(
            titular.UsuarioId,
            titular.NomeCompleto,
            titular.Email,
            titular.Documento,
            titular.TipoConta.ToString(),
            titular.CriadoEm));
    }

    public sealed record TitularResposta(
        Guid UsuarioId,
        string NomeCompleto,
        string Email,
        string Documento,
        string TipoConta,
        DateTimeOffset CriadoEm);
}
