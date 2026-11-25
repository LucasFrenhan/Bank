using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Abstracoes;
using Bank.Application.Cobrancas;
using Bank.Domain.Cobrancas;
using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Endpoints;

/// <summary>
/// Cobranças da PJ e pagamento com cartão da PF (specs/014-cartao-credito-cobrancas, US2 e US3;
/// contracts/cartoes-cobrancas-api.md) e código curto (specs/015, contrato 1.1.0). Só transporte.
/// </summary>
public static class CobrancasEndpoints
{
    public const string Grupo = "/api/v1/cobrancas";

    public static IEndpointRouteBuilder MapCobrancasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Cobranças");
        string[] politicasLojista = [AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.GerarCobrancaCartao)];
        string[] politicasPagador = [AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.PagarCobrancaCartao)];

        grupo.MapPost("/", EmitirAsync)
            .RequireAuthorization(politicasLojista)
            .WithName("EmitirCobranca")
            .WithSummary("Emite uma cobrança na carteira do lojista (PJ).")
            .Produces<CobrancaResposta>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Rota literal: tem precedência sobre /{cobrancaId}.
        grupo.MapGet("/minhas", ListarMinhasAsync)
            .RequireAuthorization(politicasLojista)
            .WithName("ListarMinhasCobrancas")
            .WithSummary("Cobranças emitidas pelo lojista, das mais novas para as mais antigas.")
            .Produces<PaginaCobrancasResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet("/codigo/{codigo}", ConsultarPorCodigoAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ConsultarCobrancaPorCodigo")
            .WithSummary("Dados de uma cobrança pelo código curto do link (XXXX-XXXX).")
            .WithDescription("Aceita minúsculas, espaços e o código sem hífen. Mesma resposta e visibilidade da consulta pelo identificador; código malformado ou inexistente = 404.")
            .Produces<CobrancaResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/{cobrancaId}", ConsultarAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ConsultarCobranca")
            .WithSummary("Dados de uma cobrança, para quem vai pagar ou para o emissor.")
            .WithDescription("pagamento só aparece para o emissor e o pagador; ultimaRecusa, só para o emissor.")
            .Produces<CobrancaResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{cobrancaId}/cancelar", CancelarAsync)
            .RequireAuthorization(politicasLojista)
            .WithName("CancelarCobranca")
            .WithSummary("Cancela uma cobrança pendente do próprio lojista.")
            .Produces<CobrancaResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{cobrancaId}/pagar", PagarAsync)
            .RequireAuthorization(politicasPagador)
            .WithName("PagarCobranca")
            .WithSummary("Paga uma cobrança com o cartão One Bank do próprio pagador (PF), com idempotência.")
            .WithDescription("A bandeira simulada recusa com 422: cartao-nao-autorizado, cartao-bloqueado-fatura-vencida ou limite-cartao-insuficiente. A recusa mantém a cobrança pendente e não consome a chave.")
            .ComChaveIdempotencia("UUID gerado pelo cliente; reenvios com a mesma chave devolvem o pagamento original.")
            .Produces<PagamentoResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> EmitirAsync(EmitirCobrancaRequisicao requisicao, ClaimsPrincipal usuario, EmitirCobranca casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new EmitirCobrancaComando(usuarioId, requisicao.Valor, requisicao.Descricao), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Created((string?)null, CobrancaResposta.De(resultado.Valor))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ListarMinhasAsync(
        [FromQuery] string? status,
        [FromQuery] string? pagina,
        [FromQuery] string? tamanhoPagina,
        ClaimsPrincipal usuario,
        ListarMinhasCobrancas casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ListarMinhasCobrancasConsulta(usuarioId, status, pagina, tamanhoPagina), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Ok(new PaginaCobrancasResposta(
                [.. resultado.Valor.Itens.Select(CobrancaResposta.De)], resultado.Valor.PaginaAtual, resultado.Valor.TamanhoPagina, resultado.Valor.TotalItens))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ConsultarAsync(string cobrancaId, ClaimsPrincipal usuario, ConsultarCobranca casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ConsultarCobrancaConsulta(usuarioId, cobrancaId), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(CobrancaResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> ConsultarPorCodigoAsync(string codigo, ClaimsPrincipal usuario, ConsultarCobrancaPorCodigo casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ConsultarCobrancaPorCodigoConsulta(usuarioId, codigo), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(CobrancaResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> CancelarAsync(string cobrancaId, ClaimsPrincipal usuario, CancelarCobranca casoDeUso, CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new CancelarCobrancaComando(usuarioId, cobrancaId), cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(CobrancaResposta.De(resultado.Valor)) : resultado.ParaProblema();
    }

    private static async Task<IResult> PagarAsync(
        string cobrancaId,
        [FromHeader(Name = EndpointsComuns.CabecalhoIdempotencia)] string? chaveIdempotencia,
        PagarCobrancaRequisicao requisicao,
        ClaimsPrincipal usuario,
        PagarCobranca casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(
            new PagarCobrancaComando(usuarioId, cobrancaId, chaveIdempotencia, requisicao.NomeTitularCartao, requisicao.NumeroCartao, requisicao.Validade, requisicao.Cvv),
            cancellationToken);
        return resultado.Sucesso ? TypedResults.Ok(PagamentoResposta.De(resultado.Valor.Pagamento)) : resultado.ParaProblema();
    }

    /// <summary>Campos opcionais: ausências e valores inválidos viram erros por campo.</summary>
    public sealed record EmitirCobrancaRequisicao(decimal? Valor, string? Descricao);

    /// <summary>Dados do cartão, nunca gravados; o texto do registro não os mostra (research R2).</summary>
    public sealed record PagarCobrancaRequisicao(string? NomeTitularCartao, string? NumeroCartao, string? Validade, string? Cvv)
    {
        public override string ToString() => $"{nameof(PagarCobrancaRequisicao)} {{ *** }}";
    }

    public sealed record LojistaResposta(Guid UsuarioId, string Nome, string Documento);

    public sealed record PagamentoDaCobrancaResposta(BandeiraResposta Bandeira, string UltimosDigitosCartao, string CodigoAutorizacao);

    public sealed record UltimaRecusaResposta(string Motivo, DateTimeOffset Em);

    public sealed record CobrancaResposta(
        Guid CobrancaId,
        string Codigo,
        LojistaResposta Lojista,
        decimal Valor,
        string Descricao,
        string Status,
        DateTimeOffset CriadoEm,
        DateTimeOffset? PagaEm,
        DateTimeOffset? CanceladaEm,
        PagamentoDaCobrancaResposta? Pagamento,
        UltimaRecusaResposta? UltimaRecusa)
    {
        public static CobrancaResposta De(DetalheCobranca detalhe)
        {
            ArgumentNullException.ThrowIfNull(detalhe);

            var dados = detalhe.Dados;
            return new CobrancaResposta(
                dados.CobrancaId.Valor,
                dados.Codigo.ToString(),
                new LojistaResposta(dados.LojistaUsuarioId.Valor, dados.LojistaNome, dados.LojistaDocumento),
                dados.Valor,
                dados.Descricao,
                dados.Status.ToString(),
                dados.CriadoEm,
                dados.PagaEm,
                dados.CanceladaEm,
                detalhe.Pagamento is { } p ? new PagamentoDaCobrancaResposta(BandeiraResposta.De(p.Bandeira), p.UltimosDigitosCartao, p.CodigoAutorizacao) : null,
                detalhe.UltimaRecusa is { } r ? new UltimaRecusaResposta(r.Motivo.ToString(), r.Em) : null);
        }
    }

    public sealed record PaginaCobrancasResposta(IReadOnlyList<CobrancaResposta> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);

    public sealed record PagamentoResposta(
        Guid CobrancaId,
        string Status,
        decimal Valor,
        BandeiraResposta Bandeira,
        string UltimosDigitosCartao,
        string CodigoAutorizacao,
        string LojistaNome,
        DateTimeOffset PagaEm)
    {
        public static PagamentoResposta De(PagamentoAprovado pagamento)
        {
            ArgumentNullException.ThrowIfNull(pagamento);

            return new PagamentoResposta(
                pagamento.CobrancaId.Valor,
                nameof(StatusCobranca.Paga),
                pagamento.Valor,
                BandeiraResposta.De(Domain.Cartoes.Bandeira.DeCodigo(pagamento.CodigoBandeira)!),
                pagamento.UltimosDigitos,
                pagamento.CodigoAutorizacao,
                pagamento.LojistaNome,
                pagamento.PagaEm);
        }
    }
}
