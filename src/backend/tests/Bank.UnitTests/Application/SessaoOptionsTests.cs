using Bank.Application.Autenticacao;

namespace Bank.UnitTests.Application;

/// <summary>Validação dos prazos da sessão na inicialização (specs/016-sessao-refresh-token, FR-002).</summary>
public sealed class SessaoOptionsTests
{
    private static readonly ValidadorSessaoOptions Validador = new();

    [Fact]
    public void Padroes_Validos_E_LimitesDeProducao()
    {
        var opcoes = new SessaoOptions();

        Assert.True(Validador.Validate(null, opcoes).Succeeded);
        var limites = opcoes.Limites();
        Assert.Equal(TimeSpan.FromMinutes(15), limites.Inatividade);
        Assert.Equal(TimeSpan.FromHours(8), limites.Absoluto);
        Assert.Equal(TimeSpan.FromSeconds(30), limites.ToleranciaReuso);
        Assert.Equal(TimeSpan.FromDays(7), opcoes.Retencao);
    }

    [Fact]
    public void PrazosDoE2e_Validos()
    {
        var opcoes = new SessaoOptions { InatividadeSegundos = 25, ToleranciaReusoSegundos = 5 };

        Assert.True(Validador.Validate(null, opcoes).Succeeded);
    }

    [Theory]
    [InlineData(0, 8, 30, 7, "InatividadeSegundos")]
    [InlineData(900, 0, 30, 7, "LimiteAbsolutoHoras")]
    [InlineData(900, 8, 0, 7, "ToleranciaReusoSegundos")]
    [InlineData(900, 8, 30, 0, "RetencaoDias")]
    [InlineData(32400, 8, 30, 7, "não pode passar")]
    [InlineData(25, 8, 30, 7, "deve ser menor")]
    public void Invalidos_FalhamIdentificandoAChave(int inatividade, int absoluto, int tolerancia, int retencao, string trecho)
    {
        var resultado = Validador.Validate(null, new SessaoOptions
        {
            InatividadeSegundos = inatividade,
            LimiteAbsolutoHoras = absoluto,
            ToleranciaReusoSegundos = tolerancia,
            RetencaoDias = retencao,
        });

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("Sessao:", StringComparison.Ordinal) && f.Contains(trecho, StringComparison.Ordinal));
    }
}
