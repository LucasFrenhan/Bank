using Bank.Domain.Carteiras;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;

namespace Bank.UnitTests.Domain;

/// <summary>Lançamentos da carteira por fatura e recebível (specs/014-cartao-credito-cobrancas, FR-034; research R14).</summary>
public class LancamentoCartaoTests
{
    private static readonly DateTimeOffset Agora = CartoesDeTeste.Agora;

    [Fact]
    public void PagamentoFatura_SaidaPeloValorPagoComJuros()
    {
        var cartao = CartoesDeTeste.Cartao();
        var fatura = CartoesDeTeste.Fatura(cartao);
        fatura.Lancar(CartoesDeTeste.Pagar(CartoesDeTeste.Cobranca(1000m), cartao, fatura).Compra, Agora);
        fatura.Liberar(new DateOnly(2026, 12, 8), Agora);
        var carteira = CarteiraId.Novo(Agora);
        Assert.Throws<InvalidOperationException>(() => Lancamento.DePagamentoFatura(carteira, fatura, Agora));
        fatura.Pagar(new DateOnly(2026, 12, 20), 15m, Guid.NewGuid(), Agora);

        var lancamento = Lancamento.DePagamentoFatura(carteira, fatura, Agora);

        Assert.Equal((TipoLancamento.PagamentoFatura, DirecaoLancamento.Saida, 1050.00m, "Pagamento da fatura 12/2026"),
            (lancamento.Tipo, lancamento.Direcao, lancamento.Valor, lancamento.Descricao));
        Assert.Equal((carteira, fatura.Id), (lancamento.CarteiraId, lancamento.FaturaId!.Value));
        Assert.Null(lancamento.RecebivelId);
        Assert.Null(lancamento.TransferenciaId);
    }

    [Fact]
    public void Recebivel_LiquidadoEAntecipado_EntradaPeloLiquido()
    {
        var liquidado = Recebivel.Criar(CartoesDeTeste.Cobranca(), CartoesDeTeste.Hoje, Agora);
        Assert.Throws<InvalidOperationException>(() => Lancamento.DeRecebivel(liquidado, "Café", Agora));
        liquidado.Liquidar(liquidado.DataPrevista, Agora);
        var antecipado = Recebivel.Criar(CartoesDeTeste.Cobranca(), CartoesDeTeste.Hoje, Agora);
        antecipado.Antecipar(CartoesDeTeste.Hoje, Guid.NewGuid(), Agora);

        var l = Lancamento.DeRecebivel(liquidado, "Café", Agora);
        var a = Lancamento.DeRecebivel(antecipado, "Café", Agora);

        Assert.Equal((TipoLancamento.LiquidacaoRecebivel, DirecaoLancamento.Entrada, 299.90m, "Recebimento: Café"), (l.Tipo, l.Direcao, l.Valor, l.Descricao));
        Assert.Equal((TipoLancamento.AntecipacaoRecebivel, DirecaoLancamento.Entrada, 293.90m, "Antecipação: Café"), (a.Tipo, a.Direcao, a.Valor, a.Descricao));
        Assert.Equal(liquidado.Id, l.RecebivelId);
    }

    [Fact]
    public void Recebivel_DescricaoTruncadaEm200()
    {
        var recebivel = Recebivel.Criar(CartoesDeTeste.Cobranca(), CartoesDeTeste.Hoje, Agora);
        recebivel.Liquidar(recebivel.DataPrevista, Agora);

        var descricao = Lancamento.DeRecebivel(recebivel, new string('x', 200), Agora).Descricao!;

        Assert.Equal(200, descricao.Length);
        Assert.StartsWith("Recebimento: x", descricao, StringComparison.Ordinal);
    }
}
