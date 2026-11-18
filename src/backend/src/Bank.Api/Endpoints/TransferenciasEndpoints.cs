using System.Security.Claims;
using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Abstracoes;
using Bank.Application.Transferencias;
using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Bank.Api.Endpoints;

/// <summary>
/// Transferências, consulta de destinatário, extrato (specs/006-transferencias) e consulta por
/// identificador (specs/008-consulta-transferencia; contrato v1.1.0). Só transporte: claims e cabeçalho viram comando para o caso
/// de uso; o resultado vira HTTP.
/// </summary>
public static class TransferenciasEndpoints
{
    public const string Grupo = "/api/v1/transferencias";
    public const string CabecalhoIdempotencia = "Idempotency-Key";

    public static IEndpointRouteBuilder MapTransferenciasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Transferências");
        string[] politicasPagador = [AutenticacaoExtensions.PoliticaUsuarioAutenticado, PoliticasAutorizacao.Nome(Permissao.RealizarTransferencia)];

        grupo.MapPost("/", TransferirAsync)
            .RequireAuthorization(politicasPagador)
            .WithName("Transferir")
            .WithSummary("Transfere da carteira do usuário (PF) para uma conta PF ou PJ, com idempotência.")
            .WithDescription("Reenvios com o mesmo cabeçalho Idempotency-Key devolvem 200 com o comprovante original, sem novo débito.")
            .AddOpenApiOperationTransformer(MarcarCabecalhoObrigatorio)
            .Produces<ComprovanteResposta>(StatusCodes.Status201Created)
            .Produces<ComprovanteResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Documento no corpo, nunca na URL: o caminho vai para spans e logs de acesso (R7).
        grupo.MapPost("/destinatarios/consultar", ConsultarDestinatarioAsync)
            .RequireAuthorization(politicasPagador)
            .WithName("ConsultarDestinatario")
            .WithSummary("Confirma o nome e o tipo de conta do destinatário pelo documento, antes de transferir.")
            .Produces<DestinatarioResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/extrato", ObterExtratoAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ObterExtrato")
            .WithSummary("Lista os lançamentos da carteira do usuário (PF e PJ), do mais recente para o mais antigo.")
            .Produces<ExtratoResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Sem restrição de tipo: identificador inválido vira 400 com erro no campo (spec 008, R2).
        // A rota literal /extrato tem precedência sobre o parâmetro.
        grupo.MapGet("/{transferenciaId}", ConsultarTransferenciaAsync)
            .RequireAuthorization(AutenticacaoExtensions.PoliticaUsuarioAutenticado)
            .WithName("ConsultarTransferencia")
            .WithSummary("Reabre o comprovante de uma transferência (pagador ou recebedor).")
            .WithDescription("Para o recebedor, chaveIdempotencia vem nula. Transferência inexistente ou de que o usuário não participa recebe o mesmo 404.")
            .Produces<ComprovanteResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ConsultarTransferenciaAsync(
        string transferenciaId,
        ClaimsPrincipal usuario,
        ConsultarTransferencia casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ConsultarTransferenciaConsulta(usuarioId, transferenciaId), cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Ok(ComprovanteResposta.De(resultado.Valor))
            : resultado.ParaProblema();
    }

    private static async Task<IResult> ObterExtratoAsync(
        [FromQuery] string? pagina,
        [FromQuery] string? tamanhoPagina,
        [FromQuery] string? direcao,
        ClaimsPrincipal usuario,
        ObterExtrato casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(new ObterExtratoConsulta(usuarioId, pagina, tamanhoPagina, direcao), cancellationToken);
        if (!resultado.Sucesso)
        {
            return resultado.ParaProblema();
        }

        var extrato = resultado.Valor;
        return TypedResults.Ok(new ExtratoResposta(
            [.. extrato.Itens.Select(i => new ItemExtratoResposta(
                i.LancamentoId,
                i.Tipo.ToString(),
                i.Direcao.ToString(),
                i.TransferenciaId,
                i.Contraparte is null ? null : new ContraparteResposta(i.Contraparte.Nome, i.Contraparte.Documento, i.Contraparte.TipoConta.ToString()),
                i.Valor,
                i.Descricao,
                i.CriadoEm))],
            extrato.PaginaAtual,
            extrato.TamanhoPagina,
            extrato.TotalItens));
    }

    public sealed record ContraparteResposta(string Nome, string Documento, string TipoConta);

    public sealed record ItemExtratoResposta(
        Guid LancamentoId,
        string Tipo,
        string Direcao,
        Guid? TransferenciaId,
        ContraparteResposta? Contraparte,
        decimal Valor,
        string? Descricao,
        DateTimeOffset CriadoEm);

    public sealed record ExtratoResposta(IReadOnlyList<ItemExtratoResposta> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);

    private static async Task<IResult> ConsultarDestinatarioAsync(
        ConsultaDestinatarioRequisicao requisicao,
        ClaimsPrincipal usuario,
        ConsultarDestinatario casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, requisicao.Documento, cancellationToken);
        return resultado.Sucesso
            ? TypedResults.Ok(new DestinatarioResposta(resultado.Valor.Nome, resultado.Valor.TipoConta.ToString()))
            : resultado.ParaProblema();
    }

    public sealed record ConsultaDestinatarioRequisicao(string? Documento);

    public sealed record DestinatarioResposta(string Nome, string TipoConta);

    private static async Task<IResult> TransferirAsync(
        [FromHeader(Name = CabecalhoIdempotencia)] string? chaveIdempotencia,
        TransferenciaRequisicao requisicao,
        ClaimsPrincipal usuario,
        RealizarTransferencia casoDeUso,
        CancellationToken cancellationToken)
    {
        if (!usuario.TentarObterUsuarioId(out var usuarioId))
        {
            return TypedResults.Forbid();
        }

        var resultado = await casoDeUso.ExecutarAsync(
            new RealizarTransferenciaComando(usuarioId, chaveIdempotencia, requisicao.DocumentoDestino, requisicao.Valor, requisicao.Descricao),
            cancellationToken);

        if (!resultado.Sucesso)
        {
            return resultado.ParaProblema();
        }

        var corpo = ComprovanteResposta.De(resultado.Valor.Comprovante);
        return resultado.Valor.Replay
            ? TypedResults.Ok(corpo)
            : TypedResults.Created((string?)null, corpo);
    }

    private static Task MarcarCabecalhoObrigatorio(OpenApiOperation operacao, Microsoft.AspNetCore.OpenApi.OpenApiOperationTransformerContext contexto, CancellationToken cancellationToken)
    {
        foreach (var parametro in operacao.Parameters ?? [])
        {
            if (parametro is OpenApiParameter cabecalho && cabecalho.Name == CabecalhoIdempotencia)
            {
                cabecalho.Required = true;
                cabecalho.Description = "UUID gerado pelo cliente; reenvios com a mesma chave não debitam de novo.";
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Campos opcionais: ausências e valores inválidos viram erros por campo.</summary>
    public sealed record TransferenciaRequisicao(string? DocumentoDestino, decimal? Valor, string? Descricao);

    /// <summary>Documento do pagador mascarado (contrato v1.1.0, spec 008).</summary>
    public sealed record PagadorResposta(Guid UsuarioId, string Nome, string Documento);

    public sealed record RecebedorResposta(Guid UsuarioId, string Nome, string Documento, string TipoConta);

    /// <summary>
    /// Comprovante do envio e da consulta (contrato v1.1.0): <c>chaveIdempotencia</c> é nula só na
    /// consulta feita pelo recebedor.
    /// </summary>
    public sealed record ComprovanteResposta(
        Guid TransferenciaId,
        Guid? ChaveIdempotencia,
        PagadorResposta Pagador,
        RecebedorResposta Recebedor,
        decimal Valor,
        string? Descricao,
        string Status,
        DateTimeOffset CriadoEm)
    {
        public static ComprovanteResposta De(Comprovante comprovante)
        {
            ArgumentNullException.ThrowIfNull(comprovante);

            return new ComprovanteResposta(
                comprovante.TransferenciaId,
                comprovante.ChaveIdempotencia,
                new PagadorResposta(comprovante.Pagador.UsuarioId, comprovante.Pagador.Nome, comprovante.Pagador.Documento),
                new RecebedorResposta(comprovante.Recebedor.UsuarioId, comprovante.Recebedor.Nome, comprovante.Recebedor.Documento, comprovante.Recebedor.TipoConta.ToString()),
                comprovante.Valor,
                comprovante.Descricao,
                comprovante.Status.ToString(),
                comprovante.CriadoEm);
        }
    }
}
