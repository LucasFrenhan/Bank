using Bank.Application.Abstracoes;
using Bank.Application.Cartoes;
using Bank.Application.Transferencias;
using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;
using Microsoft.Extensions.Options;

namespace Bank.Application.Faturas;

/// <summary>Fatura no dia da consulta (specs/014-cartao-credito-cobrancas, FR-025, FR-027, FR-028a).</summary>
/// <param name="ValorAPagar">Total com os juros de hoje; nulo se não estiver disponível nem vencida.</param>
public sealed record DetalheFatura(
    CompetenciaFatura Competencia,
    DateOnly Inicio,
    DateOnly Fechamento,
    DateOnly Vencimento,
    SituacaoFatura Situacao,
    decimal Total,
    int DiasAtraso,
    decimal Juros,
    decimal? ValorAPagar,
    DateTimeOffset? PagaEm,
    decimal? ValorPago,
    IReadOnlyList<ItemFatura> Lancamentos)
{
    public static DetalheFatura De(Fatura fatura, DateOnly hoje, decimal taxaJurosMensal, IReadOnlyList<ItemFatura> lancamentos)
    {
        ArgumentNullException.ThrowIfNull(fatura);

        var situacao = fatura.Situacao(hoje);
        var aPagar = situacao is SituacaoFatura.Disponivel or SituacaoFatura.Vencida ? fatura.CalcularPagamento(hoje, taxaJurosMensal) : null;

        return new DetalheFatura(
            CompetenciaFatura.DoMes(fatura.Competencia),
            fatura.Inicio,
            fatura.Fechamento,
            fatura.Vencimento,
            situacao,
            fatura.Total,
            fatura.DiasAtraso ?? aPagar?.DiasAtraso ?? 0,
            fatura.Juros ?? aPagar?.Juros ?? 0.00m,
            aPagar?.ValorAPagar,
            fatura.PagaEm,
            fatura.ValorPago,
            lancamentos);
    }
}

/// <summary>Parâmetros como chegam da query string.</summary>
public sealed record ListarFaturasConsulta(UsuarioId UsuarioId, string? Pagina, string? TamanhoPagina);

/// <summary>Faturas do cartão, da mais nova para a mais antiga, com a do ciclo atual mesmo sem compras (FR-027).</summary>
public sealed class ListarFaturas(
    ICartaoRepositorio cartoes,
    IFaturaConsultas faturas,
    IOptionsMonitor<CartoesOptions> opcoes,
    CalendarioBrasilia calendario,
    TimeProvider relogio)
{
    public async Task<Resultado<PaginaDe<DetalheFatura>>> ExecutarAsync(ListarFaturasConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var paginacao = Paginacao.Ler(consulta.Pagina, consulta.TamanhoPagina);
        if (!paginacao.Sucesso)
        {
            return Resultado.Falha<PaginaDe<DetalheFatura>>(paginacao.Erros);
        }

        var cartao = await cartoes.ObterDoUsuarioAsync(consulta.UsuarioId, cancellationToken);
        if (cartao is null)
        {
            return Resultado.Falha<PaginaDe<DetalheFatura>>(ErrosCartao.NaoEncontrado);
        }

        var agora = relogio.AgoraEmMicrossegundos();
        var hoje = calendario.Hoje(agora).Data;
        var atual = CompetenciaFatura.De(hoje);
        var todas = (await faturas.ListarAsync(cartao.Id, cancellationToken)).ToList();
        if (!todas.Exists(f => f.Competencia == atual.PrimeiroDiaDoMes))
        {
            todas.Add(Fatura.Abrir(cartao.Id, atual, agora)); // só para exibição: nada é gravado
        }

        var pagina = paginacao.Valor;
        var taxa = opcoes.CurrentValue.JurosAtrasoMensalPercentual;
        return Resultado.Ok(new PaginaDe<DetalheFatura>(
            [.. todas.OrderByDescending(f => f.Competencia).Skip(pagina.Deslocamento).Take(pagina.Tamanho).Select(f => DetalheFatura.De(f, hoje, taxa, []))],
            pagina.Numero,
            pagina.Tamanho,
            todas.Count));
    }
}

public sealed record ObterFaturaConsulta(UsuarioId UsuarioId, string? Competencia);

/// <summary>Detalhe de uma fatura do próprio cartão (FR-027).</summary>
public sealed class ObterFatura(
    ICartaoRepositorio cartoes,
    IFaturaConsultas faturas,
    IOptionsMonitor<CartoesOptions> opcoes,
    CalendarioBrasilia calendario,
    TimeProvider relogio)
{
    public async Task<Resultado<DetalheFatura>> ExecutarAsync(ObterFaturaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var competencia = CompetenciaFatura.Criar(consulta.Competencia);
        if (!competencia.Sucesso)
        {
            return Resultado.Falha<DetalheFatura>(competencia.Erros);
        }

        var cartao = await cartoes.ObterDoUsuarioAsync(consulta.UsuarioId, cancellationToken);
        return cartao is null
            ? Resultado.Falha<DetalheFatura>(ErrosCartao.NaoEncontrado)
            : await DetalharAsync(cartao.Id, competencia.Valor, cancellationToken);
    }

    internal async Task<Resultado<DetalheFatura>> DetalharAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken)
    {
        var agora = relogio.AgoraEmMicrossegundos();
        var hoje = calendario.Hoje(agora).Data;
        var taxa = opcoes.CurrentValue.JurosAtrasoMensalPercentual;

        var fatura = await faturas.ObterAsync(cartaoId, competencia, cancellationToken);
        if (fatura is not null)
        {
            return Resultado.Ok(DetalheFatura.De(fatura, hoje, taxa, await faturas.ListarLancamentosAsync(fatura.Id, cancellationToken)));
        }

        return competencia == CompetenciaFatura.De(hoje)
            ? Resultado.Ok(DetalheFatura.De(Fatura.Abrir(cartaoId, competencia, agora), hoje, taxa, []))
            : Resultado.Falha<DetalheFatura>(ErrosFatura.NaoEncontrada);
    }
}
