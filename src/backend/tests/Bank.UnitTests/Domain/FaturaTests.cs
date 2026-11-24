using Bank.Domain.Cartoes;

namespace Bank.UnitTests.Domain;

/// <summary>Ciclo, situação, pagamento e juros (specs/014-cartao-credito-cobrancas, FR-024 a FR-028a).</summary>
public class FaturaTests
{
    private static readonly DateTimeOffset Agora = CartoesDeTeste.Agora;

    [Theory]
    [InlineData("2026-12-07", "2026-12")]
    [InlineData("2026-12-08", "2027-01")]
    [InlineData("2026-12-31", "2027-01")]
    [InlineData("2027-01-07", "2027-01")]
    [InlineData("2027-03-07", "2027-03")]
    public void Competencia_PelaDataDaCompra(string data, string competencia) =>
        Assert.Equal(competencia, CompetenciaFatura.De(Data(data)).ToString());

    [Fact]
    public void Competencia_DatasDoCiclo()
    {
        var janeiro = CompetenciaFatura.Criar("2027-01").Valor;

        Assert.Equal(Data("2026-12-08"), janeiro.Inicio);
        Assert.Equal(Data("2027-01-07"), janeiro.Fechamento);
        Assert.Equal(Data("2027-01-11"), janeiro.Vencimento); // 10/01/2027 é domingo
        Assert.Equal(Data("2026-12-10"), CompetenciaFatura.Criar("2026-12").Valor.Vencimento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("202612")]
    [InlineData("1999-12")]
    public void Competencia_Invalida(string? texto) =>
        Assert.Equal(CompetenciaFatura.Campo, Assert.Single(CompetenciaFatura.Criar(texto).Erros).Campo);

    [Fact]
    public void Situacao_AcompanhaAsDatas()
    {
        var (fatura, _) = FaturaComCompra(1000m);

        Assert.Equal(SituacaoFatura.Aberta, fatura.Situacao(Data("2026-12-07")));
        Assert.Equal(SituacaoFatura.Fechada, fatura.Situacao(Data("2026-12-08")));

        fatura.Liberar(Data("2026-12-08"), Agora);

        Assert.Equal(SituacaoFatura.Disponivel, fatura.Situacao(Data("2026-12-08")));
        Assert.Equal(SituacaoFatura.Disponivel, fatura.Situacao(Data("2026-12-10")));
        Assert.Equal(SituacaoFatura.Vencida, fatura.Situacao(Data("2026-12-11")));

        fatura.Pagar(Data("2026-12-11"), 15m, Guid.NewGuid(), Agora);

        Assert.Equal(SituacaoFatura.Paga, fatura.Situacao(Data("2027-12-31")));
    }

    [Fact]
    public void Lancar_SomaNoTotalECopiaOsDados()
    {
        var cartao = CartoesDeTeste.Cartao();
        var fatura = CartoesDeTeste.Fatura(cartao);
        var compra = CartoesDeTeste.Pagar(CartoesDeTeste.Cobranca(10.50m), cartao, fatura).Compra;
        var outra = CartoesDeTeste.Pagar(CartoesDeTeste.Cobranca(0.01m), cartao, fatura).Compra;

        var lancamento = fatura.Lancar(compra, Agora);
        fatura.Lancar(outra, Agora);

        Assert.Equal(10.51m, fatura.Total);
        Assert.Equal(("Cafeteria Ltda", "Café Especial", 10.50m, CartoesDeTeste.Hoje), (lancamento.Estabelecimento, lancamento.Descricao, lancamento.Valor, lancamento.DataCompra));
        Assert.Equal((fatura.Id, compra.Id), (lancamento.FaturaId, lancamento.CompraId));
    }

    [Fact]
    public void Lancar_RecusaCompraDeOutraFaturaEFaturaLiberada()
    {
        var (fatura, compra) = FaturaComCompra(10m);
        var outra = CartoesDeTeste.Fatura(CartoesDeTeste.Cartao());
        Assert.Throws<InvalidOperationException>(() => outra.Lancar(compra, Agora));

        fatura.Liberar(Data("2026-12-08"), Agora);

        Assert.Throws<InvalidOperationException>(() => fatura.Lancar(compra, Agora));
    }

    [Fact]
    public void Liberar_SoDepoisDoFechamento_EUmaVez()
    {
        var (fatura, _) = FaturaComCompra(10m);
        Assert.Throws<InvalidOperationException>(() => fatura.Liberar(Data("2026-12-07"), Agora));

        fatura.Liberar(Data("2026-12-08"), Agora);

        Assert.Equal(Data("2026-12-10"), fatura.Vencimento);
        Assert.Throws<InvalidOperationException>(() => fatura.Liberar(Data("2026-12-08"), Agora));
    }

    [Fact]
    public void Liberar_DepoisDoVencimento_VenceNoDiaDaLiberacao_SemJuros()
    {
        var (fatura, _) = FaturaComCompra(1000m);

        fatura.Liberar(Data("2026-12-15"), Agora);

        Assert.Equal(Data("2026-12-15"), fatura.Vencimento);
        Assert.Equal(SituacaoFatura.Disponivel, fatura.Situacao(Data("2026-12-15")));
        Assert.Equal(0m, fatura.CalcularPagamento(Data("2026-12-15"), 15m).Juros);
        Assert.Equal(1, fatura.CalcularPagamento(Data("2026-12-16"), 15m).DiasAtraso);
    }

    [Fact]
    public void Pagar_SoDisponivelOuVencida()
    {
        var (fatura, _) = FaturaComCompra(10m);
        Assert.Equal(ErrosFatura.NaoDisponivel, Assert.Single(fatura.Pagar(Data("2026-12-01"), 15m, Guid.NewGuid(), Agora).Erros)); // aberta
        Assert.Equal(ErrosFatura.NaoDisponivel, Assert.Single(fatura.Pagar(Data("2026-12-08"), 15m, Guid.NewGuid(), Agora).Erros)); // fechada
        fatura.Liberar(Data("2026-12-08"), Agora);

        Assert.True(fatura.Pagar(Data("2026-12-09"), 15m, Guid.NewGuid(), Agora).Sucesso);
        Assert.Equal(ErrosFatura.NaoDisponivel, Assert.Single(fatura.Pagar(Data("2026-12-09"), 15m, Guid.NewGuid(), Agora).Erros)); // paga
    }

    [Fact]
    public void Pagar_Vencida_GuardaTaxaDiasJurosEValor()
    {
        var (fatura, _) = FaturaComCompra(1000m);
        fatura.Liberar(Data("2026-12-08"), Agora);
        var chave = Guid.NewGuid();

        var pagamento = fatura.Pagar(Data("2026-12-20"), 15m, chave, Agora).Valor;

        Assert.Equal((1000m, 10, 50.00m, 1050.00m), (pagamento.Total, pagamento.DiasAtraso, pagamento.Juros, pagamento.ValorAPagar));
        Assert.Equal((15m, 10, 50.00m, 1050.00m), (fatura.TaxaJurosMensal, fatura.DiasAtraso, fatura.Juros, fatura.ValorPago));
        Assert.Equal(chave, fatura.ChaveIdempotencia);
        Assert.Equal(Agora, fatura.PagaEm);
    }

    [Theory]
    [InlineData("1000.00", "15", 10, "50.00")]
    [InlineData("299.90", "15", 1, "1.50")]
    [InlineData("299.90", "15", 0, "0.00")]
    [InlineData("1000.00", "0", 10, "0.00")]
    [InlineData("0.01", "15", 1, "0.00")]
    [InlineData("1.00", "15", 1, "0.01")] // 0,005 → meio centavo para cima
    [InlineData("100.00", "10", 7, "2.33")]
    public void Juros_SimplesPorDia(string total, string taxa, int dias, string esperado) =>
        Assert.Equal(esperado, JurosAtraso.Calcular(Decimal(total), Decimal(taxa), dias).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static (Fatura Fatura, CompraCartao Compra) FaturaComCompra(decimal valor)
    {
        var cartao = CartoesDeTeste.Cartao();
        var fatura = CartoesDeTeste.Fatura(cartao);
        var compra = CartoesDeTeste.Pagar(CartoesDeTeste.Cobranca(valor), cartao, fatura).Compra;
        fatura.Lancar(compra, Agora);
        return (fatura, compra);
    }

    private static DateOnly Data(string texto) => DateOnly.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);

    private static decimal Decimal(string texto) => decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
}
