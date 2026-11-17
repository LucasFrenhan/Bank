using Bank.Domain.Transferencias;

namespace Bank.UnitTests.Domain;

/// <summary>FR-004 da spec 006: ≥ 0,01, até 2 casas, dentro de numeric(18,2).</summary>
public class ValorTransferenciaTests
{
    public static TheoryData<decimal> Validos() => [0.01m, 150.50m, 300m, 9_999_999_999_999_999.99m];

    public static TheoryData<decimal> Invalidos() => [0m, -1m, 0.009m, 10.005m, 10_000_000_000_000_000m];

    [Theory]
    [MemberData(nameof(Validos))]
    public void Criar_ValorValido_Aceita(decimal valor)
    {
        var resultado = ValorTransferencia.Criar(valor);

        Assert.True(resultado.Sucesso);
        Assert.Equal(valor, resultado.Valor.Valor);
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void Criar_ValorInvalido_ErroNoCampoValor(decimal valor)
    {
        Assert.Equal("valor", Assert.Single(ValorTransferencia.Criar(valor).Erros).Campo);
    }

    [Fact]
    public void Criar_Ausente_ErroNoCampoValor()
    {
        Assert.Equal("valor", Assert.Single(ValorTransferencia.Criar(null).Erros).Campo);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Almoço  ", "Almoço")]
    public void Descricao_Normaliza(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, DescricaoTransferencia.Normalizar(entrada).Valor);
    }

    [Fact]
    public void Descricao_ComMaisDe200Caracteres_Rejeita()
    {
        Assert.True(DescricaoTransferencia.Normalizar(new string('a', 200)).Sucesso);
        Assert.Equal("descricao", Assert.Single(DescricaoTransferencia.Normalizar(new string('a', 201)).Erros).Campo);
    }
}
