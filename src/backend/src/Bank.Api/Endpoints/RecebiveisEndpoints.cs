using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Abstracoes;
using Bank.Application.Recebiveis;
using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Endpoints;

/// <summary>Recebíveis da PJ (specs/014-cartao-credito-cobrancas, US6; contracts/cartoes-cobrancas-api.md). Só transporte.</summary>
public static class RecebiveisEndpoints
{
    public const string Grupo = "/api/v1/recebiveis";

    public static IEndpointRouteBuilder MapRecebiveisEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Recebíveis")
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.GerenciarRecebiveis));

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarRecebiveis")
            .WithSummary("Recebíveis do lojista, dos mais novos para os mais antigos.")
            .Produces<PaginaRecebiveisResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/{recebivelId}", ObterAsync)
            .WithName("ObterRecebivel")
            .WithSummary("Um recebível do lojista.")
            .Produces<RecebivelResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{recebivelId}/antecipar", AnteciparAsync)
            .WithName("AnteciparRecebivel")
            .WithSummary("Antecipa um recebível com taxa de 2% e crédito imediato, com idempotência.")
            .ComChaveIdempotencia("UUID gerado pelo cliente; reenvios com a mesma chave não creditam de novo.")
            .Produces<RecebivelResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        [FromQuery] string? situacao,
        [FromQuery] string? pagina,
        [FromQuery] string? tamanhoPagina,
        ClaimsPrincipal usuario,
        ListarRecebiveis casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ListarRecebiveisConsulta(usuarioId, situacao, pagina, tamanhoPagina), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Ok(new PaginaRecebiveisResposta(
                [.. resultado.Valor.Itens.Select(RecebivelResposta.De)], resultado.Valor.PaginaAtual, resultado.Valor.TamanhoPagina, resultado.Valor.TotalItens))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ObterAsync(string recebivelId, ClaimsPrincipal usuario, ObterRecebivel casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ObterRecebivelConsulta(usuarioId, recebivelId), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(RecebivelResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> AnteciparAsync(
        string recebivelId,
        [FromHeader(Name = EndpointsComuns.CabecalhoIdempotencia)] string? chaveIdempotencia,
        ClaimsPrincipal usuario,
        AnteciparRecebivel casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new AnteciparRecebivelComando(usuarioId, recebivelId, chaveIdempotencia), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(RecebivelResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    public sealed record RecebivelResposta(
        Guid RecebivelId,
        Guid CobrancaId,
        string Descricao,
        string Situacao,
        decimal ValorBruto,
        decimal Taxa,
        decimal ValorLiquido,
        DateOnly DataPrevista,
        DateTimeOffset? RecebidoEm,
        DateTimeOffset CriadoEm)
    {
        public static RecebivelResposta De(DadosRecebivel r)
        {
            ArgumentNullException.ThrowIfNull(r);
            return new RecebivelResposta(r.RecebivelId.Valor, r.CobrancaId.Valor, r.Descricao, r.Situacao.ToString(), r.ValorBruto, r.Taxa, r.ValorLiquido, r.DataPrevista, r.RecebidoEm, r.CriadoEm);
        }
    }

    public sealed record PaginaRecebiveisResposta(IReadOnlyList<RecebivelResposta> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);
}
