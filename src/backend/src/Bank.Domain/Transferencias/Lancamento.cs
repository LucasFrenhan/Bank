using Bank.Domain.Carteiras;
using Bank.Domain.Cartoes;
using Bank.Domain.Recebiveis;
using Bank.SharedKernel;

namespace Bank.Domain.Transferencias;

public enum TipoLancamento
{
    Abertura = 1,
    Transferencia = 2,

    /// <summary>Saída: pagamento de fatura do cartão (specs/014-cartao-credito-cobrancas, FR-028).</summary>
    PagamentoFatura = 3,

    /// <summary>Entrada: recebível pago ao lojista na data prevista (spec 014, FR-031).</summary>
    LiquidacaoRecebivel = 4,

    /// <summary>Entrada: recebível antecipado, líquido da taxa (spec 014, FR-032).</summary>
    AntecipacaoRecebivel = 5,
}

public enum DirecaoLancamento
{
    Entrada = 1,
    Saida = 2,
}

/// <summary>
/// Entrada imutável do razão de uma carteira (FR-008, FR-015): o saldo é a soma das entradas
/// menos as saídas. Abertura (saldo de demonstração), um dos lados de uma transferência, pagamento de
/// fatura ou crédito de recebível (specs/014-cartao-credito-cobrancas, research R14).
/// </summary>
public sealed class Lancamento
{
    public const string DescricaoAbertura = "Saldo inicial de demonstração";

    private Lancamento()
    {
    }

    public LancamentoId Id { get; private set; }

    public CarteiraId CarteiraId { get; private set; }

    public TipoLancamento Tipo { get; private set; }

    public DirecaoLancamento Direcao { get; private set; }

    public decimal Valor { get; private set; }

    public TransferenciaId? TransferenciaId { get; private set; }

    public FaturaId? FaturaId { get; private set; }

    public RecebivelId? RecebivelId { get; private set; }

    public string? Descricao { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>Entrada do saldo de demonstração atribuído no cadastro (spec 003, RN-05).</summary>
    public static Lancamento Abertura(CarteiraId carteiraId, decimal valor, DateTimeOffset agora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        return new Lancamento
        {
            Id = LancamentoId.Novo(agora),
            CarteiraId = carteiraId,
            Tipo = TipoLancamento.Abertura,
            Direcao = DirecaoLancamento.Entrada,
            Valor = Dinheiro.NormalizarEscala(valor),
            Descricao = DescricaoAbertura,
            CriadoEm = agora,
        };
    }

    /// <summary>Saída pelo valor pago (com juros, se houver) de uma fatura.</summary>
    public static Lancamento DePagamentoFatura(CarteiraId carteiraId, Fatura fatura, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(fatura);
        var valor = fatura.ValorPago ?? throw new InvalidOperationException("A fatura não foi paga.");
        var competencia = CompetenciaFatura.DoMes(fatura.Competencia);

        return new Lancamento
        {
            Id = LancamentoId.Novo(agora),
            CarteiraId = carteiraId,
            Tipo = TipoLancamento.PagamentoFatura,
            Direcao = DirecaoLancamento.Saida,
            Valor = valor,
            FaturaId = fatura.Id,
            Descricao = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pagamento da fatura {competencia.Mes:D2}/{competencia.Ano:D4}"),
            CriadoEm = agora,
        };
    }

    /// <summary>Entrada pelo valor líquido de um recebível liquidado ou antecipado.</summary>
    public static Lancamento DeRecebivel(Recebivel recebivel, string descricaoCobranca, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(recebivel);

        var (tipo, rotulo) = recebivel.Situacao switch
        {
            SituacaoRecebivel.Liquidado => (TipoLancamento.LiquidacaoRecebivel, "Recebimento"),
            SituacaoRecebivel.Antecipado => (TipoLancamento.AntecipacaoRecebivel, "Antecipação"),
            _ => throw new InvalidOperationException("O recebível ainda não foi pago."),
        };
        var descricao = $"{rotulo}: {descricaoCobranca}";

        return new Lancamento
        {
            Id = LancamentoId.Novo(agora),
            CarteiraId = recebivel.CarteiraId,
            Tipo = tipo,
            Direcao = DirecaoLancamento.Entrada,
            Valor = recebivel.ValorLiquido,
            RecebivelId = recebivel.Id,
            Descricao = descricao.Length > DescricaoTransferencia.TamanhoMaximo ? descricao[..DescricaoTransferencia.TamanhoMaximo] : descricao,
            CriadoEm = agora,
        };
    }

    internal static Lancamento DeTransferencia(
        CarteiraId carteiraId,
        DirecaoLancamento direcao,
        decimal valor,
        TransferenciaId transferenciaId,
        string? descricao,
        DateTimeOffset agora) => new()
        {
            Id = LancamentoId.Novo(agora),
            CarteiraId = carteiraId,
            Tipo = TipoLancamento.Transferencia,
            Direcao = direcao,
            Valor = valor,
            TransferenciaId = transferenciaId,
            Descricao = descricao,
            CriadoEm = agora,
        };
}
