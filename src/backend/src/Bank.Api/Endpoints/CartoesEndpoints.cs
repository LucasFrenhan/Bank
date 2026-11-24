using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Cartoes;
using Bank.Domain.Permissoes;

namespace Bank.Api.Endpoints;

/// <summary>Cartão de crédito da PF (specs/014-cartao-credito-cobrancas, US1; contracts/cartoes-cobrancas-api.md). Só transporte.</summary>
public static class CartoesEndpoints
{
    public const string Grupo = "/api/v1/cartoes";

    public static IEndpointRouteBuilder MapCartoesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Cartões")
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.UsarCartaoCredito));

        grupo.MapPost("/", SolicitarAsync)
            .WithName("SolicitarCartao")
            .WithSummary("Solicita o cartão de crédito escolhendo a bandeira (001 Visa, 002 Mastercard).")
            .WithDescription("Cada CPF tem no máximo um cartão e a bandeira não muda depois.")
            .Produces<CartaoResposta>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapGet("/meu", ObterAsync)
            .WithName("ObterMeuCartao")
            .WithSummary("Cartão do usuário, com limites; sem número completo nem CVV.")
            .Produces<CartaoResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/meu/dados", ExibirDadosAsync)
            .WithName("ExibirDadosDoCartao")
            .WithSummary("Nome, número, validade e CVV do próprio cartão, para exibição ao titular.")
            .WithDescription("Os dados não são gravados nem guardados em cache (Cache-Control: no-store).")
            .Produces<DadosCartaoResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SolicitarAsync(SolicitarCartaoRequisicao requisicao, ClaimsPrincipal usuario, SolicitarCartao casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new SolicitarCartaoComando(usuarioId, requisicao.Bandeira), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Created((string?)null, CartaoResposta.De(resultado.Valor))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ObterAsync(ClaimsPrincipal usuario, ObterMeuCartao casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(CartaoResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> ExibirDadosAsync(HttpContext http, ClaimsPrincipal usuario, ExibirDadosCartao casoDeUso, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(DadosCartaoResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    public sealed record SolicitarCartaoRequisicao(string? Bandeira);

    public sealed record CartaoResposta(
        Guid CartaoId,
        BandeiraResposta Bandeira,
        string NomeImpresso,
        string UltimosDigitos,
        string Validade,
        decimal LimiteTotal,
        decimal LimiteDisponivel,
        decimal ComprasALancar,
        DateTimeOffset CriadoEm)
    {
        public static CartaoResposta De(MeuCartao meuCartao)
        {
            ArgumentNullException.ThrowIfNull(meuCartao);

            var cartao = meuCartao.Cartao;
            return new CartaoResposta(
                cartao.Id.Valor,
                BandeiraResposta.De(meuCartao.Bandeira),
                cartao.NomeImpresso,
                cartao.UltimosDigitos,
                cartao.Validade.ToString(),
                cartao.LimiteTotal,
                cartao.LimiteDisponivel,
                meuCartao.ComprasALancar,
                cartao.CriadoEm);
        }
    }

    /// <summary>Única resposta com os dados completos; o texto do registro sai mascarado (research R2).</summary>
    public sealed record DadosCartaoResposta(string NomeImpresso, string Numero, string Validade, string Cvv, BandeiraResposta Bandeira)
    {
        public static DadosCartaoResposta De(DadosCompletosCartao dados)
        {
            ArgumentNullException.ThrowIfNull(dados);
            return new DadosCartaoResposta(dados.NomeImpresso, dados.Numero.Valor, dados.Validade.ToString(), dados.Cvv.Valor, BandeiraResposta.De(dados.Bandeira));
        }

        public override string ToString() => $"{nameof(DadosCartaoResposta)} {{ *** }}";
    }
}
