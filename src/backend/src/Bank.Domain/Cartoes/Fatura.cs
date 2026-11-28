using Bank.SharedKernel;

namespace Bank.Domain.Cartoes;

public enum SituacaoFatura
{
    Aberta = 1,
    Fechada = 2,
    Disponivel = 3,
    Vencida = 4,
    Paga = 5,
}

/// <summary>Quanto se paga por uma fatura em um dia.</summary>
public sealed record PagamentoFatura(decimal Total, int DiasAtraso, decimal Juros)
{
    public decimal ValorAPagar => Total + Juros;
}

/// <summary>
/// Fatura mensal de um cartão (specs/014-cartao-credito-cobrancas, FR-024 a FR-028a; research R5). A
/// situação é derivada das datas. Protegida pelo bloqueio do cartão (R8).
/// </summary>
public sealed class Fatura
{
    private Fatura()
    {
    }

    public FaturaId Id { get; private set; }

    public CartaoId CartaoId { get; private set; }

    /// <summary>Primeiro dia do mês da competência.</summary>
    public DateOnly Competencia { get; private set; }

    public DateOnly Inicio { get; private set; }

    public DateOnly Fechamento { get; private set; }

    /// <summary>Dia 10 ou próximo dia útil; avança para o dia da liberação se ela atrasar.</summary>
    public DateOnly Vencimento { get; private set; }

    public decimal Total { get; private set; }

    public DateTimeOffset? LiberadaEm { get; private set; }

    public DateTimeOffset? PagaEm { get; private set; }

    public decimal? TaxaJurosMensal { get; private set; }

    public int? DiasAtraso { get; private set; }

    public decimal? Juros { get; private set; }

    public decimal? ValorPago { get; private set; }

    public Guid? ChaveIdempotencia { get; private set; }

    public static Fatura Abrir(CartaoId cartaoId, CompetenciaFatura competencia, DateTimeOffset agora) => new()
    {
        Id = FaturaId.Novo(agora),
        CartaoId = cartaoId,
        Competencia = competencia.PrimeiroDiaDoMes,
        Inicio = competencia.Inicio,
        Fechamento = competencia.Fechamento,
        Vencimento = competencia.Vencimento,
        Total = 0.00m,
    };

    /// <summary>Situação no dia (Brasília).</summary>
    public SituacaoFatura Situacao(DateOnly hoje)
    {
        if (PagaEm is not null)
        {
            return SituacaoFatura.Paga;
        }

        if (LiberadaEm is not null)
        {
            return hoje > Vencimento ? SituacaoFatura.Vencida : SituacaoFatura.Disponivel;
        }

        return hoje > Fechamento ? SituacaoFatura.Fechada : SituacaoFatura.Aberta;
    }

    /// <summary>Lança a compra e soma no total; só antes da liberação e de compra desta fatura.</summary>
    public LancamentoFatura Lancar(CompraCartao compra, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(compra);
        if (compra.FaturaId != Id)
        {
            throw new InvalidOperationException("A compra é de outra fatura.");
        }

        if (LiberadaEm is not null)
        {
            throw new InvalidOperationException("Fatura já liberada não recebe lançamentos.");
        }

        Total = Dinheiro.NormalizarEscala(Total + compra.Valor);
        return LancamentoFatura.De(compra, agora);
    }

    /// <summary>
    /// Libera para pagamento depois do fechamento. Se o vencimento já passou, vence no dia da liberação,
    /// para não cobrar do cliente o atraso do sistema.
    /// </summary>
    public void Liberar(DateOnly hoje, DateTimeOffset agora)
    {
        if (hoje <= Fechamento)
        {
            throw new InvalidOperationException("A fatura só pode ser liberada depois do fechamento.");
        }

        if (LiberadaEm is not null)
        {
            throw new InvalidOperationException("A fatura já foi liberada.");
        }

        LiberadaEm = agora;
        if (hoje > Vencimento)
        {
            Vencimento = hoje;
        }
    }

    /// <summary>Valor a pagar no dia, com juros se vencida; só para fatura liberada e não paga.</summary>
    public PagamentoFatura CalcularPagamento(DateOnly hoje, decimal taxaJurosMensal)
    {
        var dias = Math.Max(0, hoje.DayNumber - Vencimento.DayNumber);
        return new PagamentoFatura(Total, dias, JurosAtraso.Calcular(Total, taxaJurosMensal, dias));
    }

    public Resultado<PagamentoFatura> Pagar(DateOnly hoje, decimal taxaJurosMensal, Guid chaveIdempotencia, DateTimeOffset agora)
    {
        if (Situacao(hoje) is not (SituacaoFatura.Disponivel or SituacaoFatura.Vencida))
        {
            return Resultado.Falha<PagamentoFatura>(ErrosFatura.NaoDisponivel);
        }

        var pagamento = CalcularPagamento(hoje, taxaJurosMensal);
        PagaEm = agora;
        TaxaJurosMensal = taxaJurosMensal;
        DiasAtraso = pagamento.DiasAtraso;
        Juros = pagamento.Juros;
        ValorPago = pagamento.ValorAPagar;
        ChaveIdempotencia = chaveIdempotencia;
        return Resultado.Ok(pagamento);
    }
}
