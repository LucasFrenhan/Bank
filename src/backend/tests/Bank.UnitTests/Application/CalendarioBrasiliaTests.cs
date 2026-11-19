using Bank.Application.Transferencias;

namespace Bank.UnitTests.Application;

/// <summary>Dia civil de Brasília (specs/009-limite-transferencia, FR-002; research R4).</summary>
public class CalendarioBrasiliaTests
{
    private readonly CalendarioBrasilia _calendario = new();

    [Fact]
    public void QuaseMeiaNoiteEmBrasilia_JaDiaSeguinteEmUtc_ContaParaODiaDeBrasilia()
    {
        var dia = _calendario.Hoje(new DateTimeOffset(2026, 11, 18, 2, 59, 59, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 17), dia.Data);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), dia.InicioUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero), dia.FimUtc);
    }

    [Fact]
    public void MeiaNoiteEmBrasilia_ComecaONovoDia()
    {
        var dia = _calendario.Hoje(new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 18), dia.Data);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero), dia.InicioUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 3, 0, 0, TimeSpan.Zero), dia.FimUtc);
    }

    [Fact]
    public void FimExclusivo_ContemOInstanteAnteriorENaoOFim()
    {
        var dia = _calendario.Hoje(new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.Zero));

        Assert.True(dia.Contem(dia.FimUtc.AddTicks(-1)));
        Assert.False(dia.Contem(dia.FimUtc));
        Assert.True(dia.Contem(dia.InicioUtc));
    }
}
