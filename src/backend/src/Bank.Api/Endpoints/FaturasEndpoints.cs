using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Faturas;
using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Endpoints;

/// <summary>Faturas do cartão da PF (specs/014-cartao-credito-cobrancas, US4 e US5; contracts/cartoes-cobrancas-api.md). Só transporte.</summary>
public static class FaturasEndpoints
{
    public const string Grupo = "/api/v1/faturas";

    public static IEndpointRouteBuilder MapFaturasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Faturas")
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.UsarCartaoCredito));

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarFaturas")
            .WithSummary("Faturas do cartão, da mais nova para a mais antiga, com a do ciclo atual.")
            .Produces<PaginaFaturasResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/{competencia}", ObterAsync)
            .WithName("ObterFatura")
            .WithSummary("Fatura do mês (AAAA-MM), com os lançamentos.")
            .WithDescription("valorAPagar traz o total com os juros de hoje em faturas disponíveis ou vencidas.")
            .Produces<FaturaDetalhadaResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{competencia}/pagar", PagarAsync)
            .WithName("PagarFatura")
            .WithSummary("Paga o total da fatura com o saldo da carteira, com idempotência.")
            .WithDescription("Fatura vencida inclui juros por dia de atraso. O limite usado pelas compras volta a ficar disponível.")
            .ComChaveIdempotencia("UUID gerado pelo cliente; reenvios com a mesma chave não debitam de novo.")
            .Produces<FaturaDetalhadaResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        [FromQuery] string? pagina, [FromQuery] string? tamanhoPagina, ClaimsPrincipal usuario, ListarFaturas casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ListarFaturasConsulta(usuarioId, pagina, tamanhoPagina), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Ok(new PaginaFaturasResposta(
                [.. resultado.Valor.Itens.Select(FaturaResposta.De)], resultado.Valor.PaginaAtual, resultado.Valor.TamanhoPagina, resultado.Valor.TotalItens))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ObterAsync(string competencia, ClaimsPrincipal usuario, ObterFatura casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ObterFaturaConsulta(usuarioId, competencia), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(FaturaDetalhadaResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> PagarAsync(
        string competencia,
        [FromHeader(Name = EndpointsComuns.CabecalhoIdempotencia)] string? chaveIdempotencia,
        ClaimsPrincipal usuario,
        PagarFatura casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new PagarFaturaComando(usuarioId, competencia, chaveIdempotencia), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(FaturaDetalhadaResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    /// <summary>Fatura sem lançamentos, para a lista.</summary>
    public sealed record FaturaResposta(
        string Competencia,
        DateOnly Inicio,
        DateOnly Fechamento,
        DateOnly Vencimento,
        string Situacao,
        decimal Total,
        int DiasAtraso,
        decimal Juros,
        decimal? ValorAPagar,
        DateTimeOffset? PagaEm,
        decimal? ValorPago)
    {
        public static FaturaResposta De(DetalheFatura f)
        {
            ArgumentNullException.ThrowIfNull(f);
            return new FaturaResposta(f.Competencia.ToString(), f.Inicio, f.Fechamento, f.Vencimento, f.Situacao.ToString(), f.Total, f.DiasAtraso, f.Juros, f.ValorAPagar, f.PagaEm, f.ValorPago);
        }
    }

    public sealed record PaginaFaturasResposta(IReadOnlyList<FaturaResposta> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);

    public sealed record LancamentoFaturaResposta(string Estabelecimento, string Descricao, decimal Valor, DateOnly DataCompra);

    public sealed record FaturaDetalhadaResposta(
        string Competencia,
        DateOnly Inicio,
        DateOnly Fechamento,
        DateOnly Vencimento,
        string Situacao,
        decimal Total,
        int DiasAtraso,
        decimal Juros,
        decimal? ValorAPagar,
        DateTimeOffset? PagaEm,
        decimal? ValorPago,
        IReadOnlyList<LancamentoFaturaResposta> Lancamentos)
    {
        public static FaturaDetalhadaResposta De(DetalheFatura f)
        {
            ArgumentNullException.ThrowIfNull(f);
            return new FaturaDetalhadaResposta(
                f.Competencia.ToString(), f.Inicio, f.Fechamento, f.Vencimento, f.Situacao.ToString(), f.Total, f.DiasAtraso, f.Juros, f.ValorAPagar, f.PagaEm, f.ValorPago,
                [.. f.Lancamentos.Select(l => new LancamentoFaturaResposta(l.Estabelecimento, l.Descricao, l.Valor, l.DataCompra))]);
        }
    }
}
