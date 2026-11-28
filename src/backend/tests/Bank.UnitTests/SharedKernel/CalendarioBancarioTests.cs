using Bank.SharedKernel.Calendario;

namespace Bank.UnitTests.SharedKernel;

/// <summary>Dias úteis bancários (specs/014-cartao-credito-cobrancas, FR-022a; research R12).</summary>
public class CalendarioBancarioTests
{
    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 4, 21)]
    [InlineData(2026, 5, 1)]
    [InlineData(2026, 9, 7)]
    [InlineData(2026, 10, 12)]
    [InlineData(2026, 11, 2)]
    [InlineData(2027, 11, 15)]
    [InlineData(2026, 11, 20)]
    [InlineData(2026, 12, 25)]
    [InlineData(2027, 2, 8)]
    [InlineData(2027, 2, 9)]
    [InlineData(2027, 3, 26)]
    [InlineData(2027, 5, 27)]
    [InlineData(2026, 2, 16)]
    [InlineData(2026, 2, 17)]
    [InlineData(2026, 4, 3)]
    [InlineData(2026, 6, 4)]
    public void Feriado_NaoEhDiaUtil(int ano, int mes, int dia)
    {
        var data = new DateOnly(ano, mes, dia);

        Assert.Contains(data, CalendarioBancario.Feriados(ano));
        Assert.False(CalendarioBancario.EhDiaUtil(data));
    }

    [Theory]
    [InlineData(2026, 11, 28)]
    [InlineData(2026, 11, 29)]
    public void FimDeSemana_NaoEhDiaUtil(int ano, int mes, int dia) =>
        Assert.False(CalendarioBancario.EhDiaUtil(new DateOnly(ano, mes, dia)));

    [Theory]
    [InlineData(2027, 2, 10)] // Quarta-feira de Cinzas
    [InlineData(2026, 12, 24)]
    [InlineData(2026, 12, 31)]
    [InlineData(2026, 11, 24)]
    public void DiaComum_EhUtil(int ano, int mes, int dia) =>
        Assert.True(CalendarioBancario.EhDiaUtil(new DateOnly(ano, mes, dia)));

    [Fact]
    public void ProximoDiaUtil_DomingoVaiParaSegunda_EDiaUtilFicaIgual()
    {
        Assert.Equal(new DateOnly(2027, 1, 11), CalendarioBancario.ProximoDiaUtil(new DateOnly(2027, 1, 10)));
        Assert.Equal(new DateOnly(2026, 12, 10), CalendarioBancario.ProximoDiaUtil(new DateOnly(2026, 12, 10)));
    }

    [Fact]
    public void ProximoDiaUtil_PulaFeriadoSeguidoDeFimDeSemana() =>
        Assert.Equal(new DateOnly(2026, 12, 28), CalendarioBancario.ProximoDiaUtil(new DateOnly(2026, 12, 25)));

    [Fact]
    public void SomarDiasUteis_ExemploDaSpec() =>
        Assert.Equal(new DateOnly(2027, 1, 7), CalendarioBancario.SomarDiasUteis(new DateOnly(2026, 11, 24), 30));

    [Fact]
    public void SomarDiasUteis_PartidaEmDiaNaoUtil_ContaAPartirDoSeguinte() =>
        Assert.Equal(new DateOnly(2026, 11, 30), CalendarioBancario.SomarDiasUteis(new DateOnly(2026, 11, 28), 1));

    [Fact]
    public void SomarDiasUteis_QuantidadeInvalida_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarioBancario.SomarDiasUteis(new DateOnly(2026, 11, 24), 0));
}
