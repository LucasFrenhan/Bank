using Bank.Domain.Cobrancas;

namespace Bank.UnitTests.Domain;

/// <summary>Código curto da cobrança (specs/015-frontend-cartao-credito, FR-009a; research R1).</summary>
public class CodigoCobrancaTests
{
    [Fact]
    public void Novo_OitoCaracteresDoAlfabeto_ENaoRepete()
    {
        var codigos = Enumerable.Range(0, 500).Select(_ => CodigoCobranca.Novo().Valor).ToList();

        Assert.All(codigos, c => Assert.Matches("^[23456789ABCDEFGHJKMNPQRSTVWXYZ]{8}$", c));
        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Novo_UsaTodoOAlfabeto()
    {
        var usados = Enumerable.Range(0, 1000).SelectMany(_ => CodigoCobranca.Novo().Valor).Distinct().Order();

        Assert.Equal(CodigoCobranca.Alfabeto.Order(), usados);
    }

    [Theory]
    [InlineData("7K3F9QXM")]
    [InlineData("7K3F-9QXM")]
    [InlineData("7k3f-9qxm")]
    [InlineData("  7k3f 9qxm ")]
    [InlineData("7-K-3-F-9-Q-X-M")]
    public void Criar_Normaliza(string texto)
    {
        var codigo = CodigoCobranca.Criar(texto);

        Assert.NotNull(codigo);
        Assert.Equal(("7K3F9QXM", "7K3F-9QXM"), (codigo.Valor, codigo.ToString()));
        Assert.Equal(CodigoCobranca.Criar("7K3F9QXM"), codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7K3F9QX")]
    [InlineData("7K3F9QXMA")]
    [InlineData("0K3F9QXM")]
    [InlineData("1K3F9QXM")]
    [InlineData("IK3F9QXM")]
    [InlineData("LK3F9QXM")]
    [InlineData("OK3F9QXM")]
    [InlineData("UK3F9QXM")]
    [InlineData("iK3F9QXM")]
    [InlineData("7K3F9QX*")]
    [InlineData("ÇK3F9QXM")]
    [InlineData("7K3F/9QXM")]
    [InlineData("ıK3F9QXM")]
    public void Criar_Malformado_Nulo(string? texto) => Assert.Null(CodigoCobranca.Criar(texto));
}
