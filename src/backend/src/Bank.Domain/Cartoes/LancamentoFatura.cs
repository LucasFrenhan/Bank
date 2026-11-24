namespace Bank.Domain.Cartoes;

/// <summary>Item imutável da fatura, com o estabelecimento e o valor (specs/014-cartao-credito-cobrancas, FR-026).</summary>
public sealed class LancamentoFatura
{
    private LancamentoFatura()
    {
    }

    public LancamentoFaturaId Id { get; private set; }

    public FaturaId FaturaId { get; private set; }

    public CompraCartaoId CompraId { get; private set; }

    public string Estabelecimento { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public DateOnly DataCompra { get; private set; }

    public DateTimeOffset LancadoEm { get; private set; }

    internal static LancamentoFatura De(CompraCartao compra, DateTimeOffset agora) => new()
    {
        Id = LancamentoFaturaId.Novo(agora),
        FaturaId = compra.FaturaId,
        CompraId = compra.Id,
        Estabelecimento = compra.Estabelecimento,
        Descricao = compra.Descricao,
        Valor = compra.Valor,
        DataCompra = compra.DataCompra,
        LancadoEm = agora,
    };
}
