using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Edge cases de CPF da spec (RN-03, FR-004, FR-006; SC-003).</summary>
public class CpfTests
{
    [Theory]
    [InlineData("52998224725", "52998224725")]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("  529.982.247-25  ", "52998224725")]
    [InlineData("12345678909", "12345678909")]   // exemplo da issue #5
    [InlineData("01234567890", "01234567890")]   // zero à esquerda preservado; 2º DV = 0 (resto < 2)
    public void Criar_CpfValido_NormalizaSoComDigitos(string entrada, string esperado)
    {
        var resultado = Cpf.Criar(entrada);

        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, resultado.Valor.Valor);
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("33333333333")]
    [InlineData("44444444444")]
    [InlineData("55555555555")]
    [InlineData("66666666666")]
    [InlineData("77777777777")]
    [InlineData("88888888888")]
    [InlineData("99999999999")]
    public void Criar_DigitosRepetidos_Rejeita(string entrada) => AssertInvalido(entrada);

    [Theory]
    [InlineData("52998224735")]   // 1º DV errado
    [InlineData("52998224724")]   // 2º DV errado
    [InlineData("5299822472")]    // 10 dígitos
    [InlineData("529982247250")]  // 12 dígitos
    [InlineData("5299822472A")]   // letra
    [InlineData("529/982/247-25")] // caractere não permitido para CPF
    [InlineData("529 982 247 25")] // espaços internos
    [InlineData("")]
    [InlineData(null)]
    public void Criar_FormatoOuDvInvalido_Rejeita(string? entrada) => AssertInvalido(entrada);

    private static void AssertInvalido(string? entrada)
    {
        var resultado = Cpf.Criar(entrada);

        Assert.False(resultado.Sucesso);
        Assert.Equal("documento", Assert.Single(resultado.Erros).Campo);
    }
}
