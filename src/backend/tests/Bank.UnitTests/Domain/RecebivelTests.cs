using Bank.Domain.Recebiveis;

namespace Bank.UnitTests.Domain;

/// <summary>Liquidação e antecipação (specs/014-cartao-credito-cobrancas, FR-030 a FR-032).</summary>
public class RecebivelTests
{
    private static readonly DateTimeOffset Agora = CartoesDeTeste.Agora;
    private static readonly DateOnly Prevista = new(2027, 1, 7);

    [Fact]
    public void Criar_ValorIntegralNoTrigesimoDiaUtil()
    {
        var recebivel = Novo();

        Assert.Equal((Prevista, SituacaoRecebivel.Pendente, 299.90m, 0m, 299.90m),
            (recebivel.DataPrevista, recebivel.Situacao, recebivel.ValorBruto, recebivel.Taxa, recebivel.ValorLiquido));
    }

    [Fact]
    public void Liquidar_SoNaDataPrevistaOuDepois_EUmaVez()
    {
        var recebivel = Novo();

        Assert.False(recebivel.Liquidar(Prevista.AddDays(-1), Agora));
        Assert.True(recebivel.Liquidar(Prevista, Agora));
        Assert.False(recebivel.Liquidar(Prevista, Agora));
        Assert.Equal((SituacaoRecebivel.Liquidado, 299.90m, Agora), (recebivel.Situacao, recebivel.ValorLiquido, recebivel.RecebidoEm!.Value));
    }

    [Theory]
    [InlineData("299.90", "6.00", "293.90")]
    [InlineData("0.01", "0.00", "0.01")]
    [InlineData("100.00", "2.00", "98.00")]
    [InlineData("0.25", "0.01", "0.24")] // 0,005 → meio centavo para cima
    public void Antecipar_DescontaDoisPorCento(string bruto, string taxa, string liquido)
    {
        var recebivel = Novo(Decimal(bruto));
        var chave = Guid.NewGuid();

        Assert.True(recebivel.Antecipar(Prevista.AddDays(-1), chave, Agora).Sucesso);

        Assert.Equal((Decimal(taxa), Decimal(liquido), SituacaoRecebivel.Antecipado), (recebivel.Taxa, recebivel.ValorLiquido, recebivel.Situacao));
        Assert.Equal((chave, Agora), (recebivel.ChaveIdempotencia!.Value, recebivel.RecebidoEm!.Value));
    }

    [Fact]
    public void Antecipar_NoDiaPrevisto_Recusa()
    {
        var recebivel = Novo();

        Assert.Equal(ErrosRecebivel.NaoAntecipavel, Assert.Single(recebivel.Antecipar(Prevista, Guid.NewGuid(), Agora).Erros));
        Assert.Equal((SituacaoRecebivel.Pendente, 0m), (recebivel.Situacao, recebivel.Taxa));
    }

    [Fact]
    public void Antecipado_NaoLiquidaNemAntecipaDeNovo()
    {
        var recebivel = Novo();
        recebivel.Antecipar(CartoesDeTeste.Hoje, Guid.NewGuid(), Agora);

        Assert.False(recebivel.Liquidar(Prevista, Agora));
        Assert.False(recebivel.Antecipar(CartoesDeTeste.Hoje, Guid.NewGuid(), Agora).Sucesso);
    }

    [Fact]
    public void Liquidado_NaoAntecipa()
    {
        var recebivel = Novo();
        recebivel.Liquidar(Prevista, Agora);

        Assert.False(recebivel.Antecipar(CartoesDeTeste.Hoje, Guid.NewGuid(), Agora).Sucesso);
    }

    private static Recebivel Novo(decimal valor = 299.90m) => Recebivel.Criar(CartoesDeTeste.Cobranca(valor), CartoesDeTeste.Hoje, Agora);

    private static decimal Decimal(string texto) => decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
}
