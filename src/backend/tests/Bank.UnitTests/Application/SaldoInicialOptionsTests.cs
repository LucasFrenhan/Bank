using Bank.Application.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>FR-013: valores ≥ 0, até 2 casas e zero em Production.</summary>
public class SaldoInicialOptionsTests
{
    [Fact]
    public void ForaDeProducao_ValoresDeDemonstracao_Validos()
    {
        var resultado = Validar(producao: false, pf: 10.00m, pj: 0.00m);

        Assert.True(resultado.Succeeded);
    }

    [Theory]
    [InlineData(-0.01, 0)]
    [InlineData(0, -1)]
    public void Negativo_Invalido(decimal pf, decimal pj)
    {
        var resultado = Validar(producao: false, pf, pj);

        Assert.True(resultado.Failed);
        Assert.Contains("negativo", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MaisDeDuasCasas_Invalido()
    {
        var resultado = Validar(producao: false, pf: 10.001m, pj: 0m);

        Assert.True(resultado.Failed);
        Assert.Contains("Carteiras:SaldoInicial:PessoaFisica", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(0, 0.01)]
    public void EmProducao_SaldoDiferenteDeZero_Invalido(decimal pf, decimal pj)
    {
        var resultado = Validar(producao: true, pf, pj);

        Assert.True(resultado.Failed);
        Assert.Contains("Production", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EmProducao_Zero_Valido()
    {
        Assert.True(Validar(producao: true, pf: 0m, pj: 0m).Succeeded);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validar(bool producao, decimal pf, decimal pj) =>
        new ValidadorSaldoInicialOptions(producao).Validate(null, new SaldoInicialOptions { PessoaFisica = pf, PessoaJuridica = pj });
}
