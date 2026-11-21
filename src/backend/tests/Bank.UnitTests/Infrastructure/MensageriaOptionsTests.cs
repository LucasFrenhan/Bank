using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Configuração da mensageria (specs/010-mensageria-rabbitmq, research R11).</summary>
public class MensageriaOptionsTests
{
    private static MensageriaOptions Padroes() => new()
    {
        Host = "localhost",
        VirtualHost = "bank",
        Usuario = "bank",
        Senha = "segredo",
        IntervalosNovaTentativaMs = [1000, 2000, 4000],
    };

    private static bool Valida(MensageriaOptions opcoes) => new ValidadorMensageriaOptions().Validate(null, opcoes).Succeeded;

    [Fact]
    public void Padroes_Validos()
    {
        Assert.True(Valida(Padroes()));
    }

    [Theory]
    [InlineData(nameof(MensageriaOptions.Host))]
    [InlineData(nameof(MensageriaOptions.VirtualHost))]
    [InlineData(nameof(MensageriaOptions.Usuario))]
    [InlineData(nameof(MensageriaOptions.Senha))]
    public void Obrigatorio_Vazio_Invalido(string propriedade)
    {
        var opcoes = Padroes();
        typeof(MensageriaOptions).GetProperty(propriedade)!.SetValue(opcoes, " ");

        var resultado = new ValidadorMensageriaOptions().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        Assert.Contains($"Mensageria:{propriedade}", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Porta_ForaDaFaixa_Invalida(int porta)
    {
        var opcoes = Padroes();
        opcoes.Porta = porta;

        Assert.False(Valida(opcoes));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1000, 0 })]
    [InlineData(new[] { 2000, 1000 })]
    [InlineData(new[] { 1000, 1000 })]
    public void Intervalos_VaziosNaoPositivosOuNaoCrescentes_Invalidos(int[] intervalos)
    {
        var opcoes = Padroes();
        opcoes.IntervalosNovaTentativaMs = intervalos;

        Assert.False(Valida(opcoes));
    }

    [Fact]
    public void Limites_DoDespachoEConsumo_Invalidos()
    {
        Assert.False(Valida(Com(o => o.Prefetch = 0)));
        Assert.False(Valida(Com(o => o.TamanhoLoteDespacho = 0)));
        Assert.False(Valida(Com(o => o.IntervaloDespachoMs = 0)));
        Assert.False(Valida(Com(o => o.TimeoutConfirmacaoMs = 99)));
    }

    [Fact]
    public void ToString_NaoExpoeASenha()
    {
        Assert.DoesNotContain("segredo", Padroes().ToString(), StringComparison.Ordinal);
    }

    private static MensageriaOptions Com(Action<MensageriaOptions> ajuste)
    {
        var opcoes = Padroes();
        ajuste(opcoes);
        return opcoes;
    }
}
