using Bank.Domain.Cobrancas;

namespace Bank.Domain.Cartoes;

/// <summary>
/// Compra aprovada, imutável, já ligada à fatura do seu ciclo; vira lançamento da fatura no
/// processamento diário (specs/014-cartao-credito-cobrancas, FR-018, FR-024; research R5).
/// </summary>
public sealed class CompraCartao
{
    private CompraCartao()
    {
    }

    public CompraCartaoId Id { get; private set; }

    public CartaoId CartaoId { get; private set; }

    public CobrancaId CobrancaId { get; private set; }

    public AutorizacaoCartaoId AutorizacaoId { get; private set; }

    public FaturaId FaturaId { get; private set; }

    /// <summary>Razão social do lojista no momento da compra (FR-026).</summary>
    public string Estabelecimento { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    /// <summary>Dia civil de Brasília que define o ciclo (pode diferir de <see cref="AutorizadaEm"/>: research R6).</summary>
    public DateOnly DataCompra { get; private set; }

    public DateTimeOffset AutorizadaEm { get; private set; }

    public static CompraCartao De(AutorizacaoCartao autorizacao, Cobranca cobranca, Fatura fatura, string estabelecimento, DateOnly dataCompra, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);
        ArgumentNullException.ThrowIfNull(cobranca);
        ArgumentNullException.ThrowIfNull(fatura);
        ArgumentException.ThrowIfNullOrWhiteSpace(estabelecimento);
        if (autorizacao.CartaoId != fatura.CartaoId)
        {
            throw new InvalidOperationException("A autorização não é do cartão da fatura.");
        }

        return new CompraCartao
        {
            Id = CompraCartaoId.Novo(agora),
            CartaoId = fatura.CartaoId,
            CobrancaId = cobranca.Id,
            AutorizacaoId = autorizacao.Id,
            FaturaId = fatura.Id,
            Estabelecimento = estabelecimento,
            Descricao = cobranca.Descricao,
            Valor = cobranca.Valor,
            DataCompra = dataCompra,
            AutorizadaEm = agora,
        };
    }
}
