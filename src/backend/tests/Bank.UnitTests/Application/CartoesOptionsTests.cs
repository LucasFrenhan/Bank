using System.Globalization;
using Bank.Application.Cartoes;
using Bank.Application.Comum;

namespace Bank.UnitTests.Application;

/// <summary>Configuração e entradas comuns (specs/014-cartao-credito-cobrancas, FR-005, FR-012, FR-028a).</summary>
public class CartoesOptionsTests
{
    [Fact]
    public void Padroes_SaoOsDaSpec()
    {
        var opcoes = new CartoesOptions();

        Assert.Equal((5000.00m, 15.00m), (opcoes.LimitePadrao, opcoes.JurosAtrasoMensalPercentual));
        Assert.True(new ValidadorCartoesOptions().Validate(null, opcoes).Succeeded);
    }

    [Theory]
    [InlineData("0", "15", false)]
    [InlineData("-1", "15", false)]
    [InlineData("100.001", "15", false)]
    [InlineData("0.01", "0", true)]
    [InlineData("5000", "100", true)]
    [InlineData("5000", "100.01", false)]
    [InlineData("5000", "-0.01", false)]
    [InlineData("5000", "1.555", false)]
    public void Validacao(string limite, string juros, bool valida) =>
        Assert.Equal(valida, new ValidadorCartoesOptions().Validate(null, new CartoesOptions
        {
            LimitePadrao = decimal.Parse(limite, CultureInfo.InvariantCulture),
            JurosAtrasoMensalPercentual = decimal.Parse(juros, CultureInfo.InvariantCulture),
        }).Succeeded);

    [Theory]
    [InlineData(null, null, 1, 20, 0)]
    [InlineData("3", "10", 3, 10, 20)]
    [InlineData("2147483647", "100", int.MaxValue, 100, int.MaxValue)]
    public void Paginacao_Valida(string? pagina, string? tamanho, int numero, int esperado, int deslocamento)
    {
        var paginacao = Paginacao.Ler(pagina, tamanho).Valor;

        Assert.Equal((numero, esperado, deslocamento), (paginacao.Numero, paginacao.Tamanho, paginacao.Deslocamento));
    }

    [Fact]
    public void Paginacao_Invalida_TrazOsDoisErros() =>
        Assert.Equal([Paginacao.CampoPagina, Paginacao.CampoTamanho], Paginacao.Ler("0", "101").Erros.Select(e => e.Campo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ChaveEIdentificador_Invalidos(string? texto)
    {
        Assert.Equal(Entradas.CampoChave, Assert.Single(Entradas.LerChaveIdempotencia(texto).Erros).Campo);
        Assert.Equal("id", Assert.Single(Entradas.LerIdentificador(texto, "id").Erros).Campo);
    }

    [Fact]
    public void Filtro_PeloNomeExato()
    {
        Assert.Null(Entradas.LerFiltro<DayOfWeek>(null, "dia").Valor);
        Assert.Equal(DayOfWeek.Friday, Entradas.LerFiltro<DayOfWeek>("Friday", "dia").Valor);
        Assert.False(Entradas.LerFiltro<DayOfWeek>("friday", "dia").Sucesso);
        Assert.False(Entradas.LerFiltro<DayOfWeek>("5", "dia").Sucesso);
    }

    [Fact]
    public void Agora_TemPrecisaoDeMicrossegundos() =>
        Assert.Equal(0, new RelogioFixo(new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567)).AgoraEmMicrossegundos().Ticks % 10);
}
