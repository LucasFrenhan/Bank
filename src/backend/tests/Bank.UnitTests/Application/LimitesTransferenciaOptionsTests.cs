using Bank.Application.Transferencias;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.Application;

/// <summary>Configuração dos limites (specs/009-limite-transferencia, FR-005, FR-006; research R2).</summary>
public class LimitesTransferenciaOptionsTests
{
    [Fact]
    public void Padroes_Validos()
    {
        Assert.True(Validar(1000.00m, 5000.00m).Succeeded);
    }

    [Fact]
    public void Ausentes_SemLimite_Validos()
    {
        var opcoes = new LimitesTransferenciaOptions();

        Assert.True(new ValidadorLimitesTransferenciaOptions().Validate(null, opcoes).Succeeded);
        var limites = opcoes.ParaPessoaFisica();
        Assert.Null(limites.PorOperacao);
        Assert.Null(limites.Diario);
    }

    [Fact]
    public void ValorVazio_SobrescreveOPadrao_SemLimite()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transferencias:Limites:PessoaFisica:PorOperacao"] = "1000.00",
                ["Transferencias:Limites:PessoaFisica:Diario"] = "5000.00",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transferencias:Limites:PessoaFisica:PorOperacao"] = string.Empty,
            })
            .Build();

        var opcoes = configuracao.GetSection(LimitesTransferenciaOptions.Secao).Get<LimitesTransferenciaOptions>()!;

        Assert.Null(opcoes.PessoaFisica.PorOperacao);
        Assert.Equal(5000.00m, opcoes.PessoaFisica.Diario);
    }

    [Theory]
    [InlineData(0, 5000)]
    [InlineData(-1, 5000)]
    [InlineData(1000, 0)]
    public void ZeroOuNegativo_Invalido(decimal porOperacao, decimal diario)
    {
        var resultado = Validar(porOperacao, diario);

        Assert.True(resultado.Failed);
        Assert.Contains("maior que zero", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MaisDeDuasCasas_Invalido()
    {
        var resultado = Validar(1000.001m, 5000m);

        Assert.True(resultado.Failed);
        Assert.Contains("Transferencias:Limites:PessoaFisica:PorOperacao", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PorOperacaoMaiorQueDiario_Invalido()
    {
        var resultado = Validar(6000m, 5000m);

        Assert.True(resultado.Failed);
        Assert.Contains("Transferencias:Limites:PessoaFisica:Diario", resultado.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validar(decimal? porOperacao, decimal? diario) =>
        new ValidadorLimitesTransferenciaOptions().Validate(null, new LimitesTransferenciaOptions
        {
            PessoaFisica = new LimitesPerfilOptions { PorOperacao = porOperacao, Diario = diario },
        });
}
