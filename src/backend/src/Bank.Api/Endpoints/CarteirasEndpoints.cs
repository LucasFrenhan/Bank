using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Carteiras;

namespace Bank.Api.Endpoints;

/// <summary>
/// Consulta da própria carteira (US3; contracts/usuarios-carteiras-api.md; dados bancários desde a
/// specs/012-dados-bancarios-carteira, contracts/carteira-minha.md 1.1.0). O usuário vem do
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
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
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
            carteira.CarteiraId.Valor,
            carteira.UsuarioId.Valor,
            carteira.NomeTitular,
            carteira.TipoConta.ToString(),
            carteira.Saldo,
            carteira.Moeda,
            carteira.AtualizadoEm,
            new ContaBancariaResposta(
                new BancoResposta(carteira.ContaBancaria.CodigoBanco, carteira.ContaBancaria.NomeBanco),
                carteira.ContaBancaria.Agencia,
                carteira.ContaBancaria.Numero,
                carteira.ContaBancaria.Digito,
                carteira.ContaBancaria.Tipo.ToString())));
    }

    public sealed record MinhaCarteiraResposta(
        Guid CarteiraId,
        Guid UsuarioId,
        string NomeTitular,
        string TipoConta,
        decimal Saldo,
        string Moeda,
        DateTimeOffset AtualizadoEm,
        ContaBancariaResposta ContaBancaria);

    /// <summary>Dados bancários da conta, todos como texto (specs/012-dados-bancarios-carteira, contrato 1.1.0).</summary>
    public sealed record ContaBancariaResposta(
        BancoResposta Banco,
        string Agencia,
        string Numero,
        string Digito,
        string Tipo);

    public sealed record BancoResposta(string Codigo, string Nome);
}
