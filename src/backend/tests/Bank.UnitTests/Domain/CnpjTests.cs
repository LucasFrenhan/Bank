using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>
/// Edge cases de CNPJ numérico e alfanumérico (IN RFB nº 2.229/2024; RN-03, FR-005, FR-006;
/// research R2; SC-003).
/// </summary>
public class CnpjTests
{
    [Theory]
    [InlineData("11222333000181", "11222333000181")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("12ABC34501DE35", "12ABC34501DE35")]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12abc34501de35", "12ABC34501DE35")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    public void Criar_CnpjValido_NormalizaSemMascaraEmMaiusculas(string entrada, string esperado)
    {
        var resultado = Cnpj.Criar(entrada);

        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, resultado.Valor.Valor);
    }

    [Theory]
    [InlineData("12ABC34501DE36")]  // alfanumérico, 2º DV errado
    [InlineData("12ABC34501DE45")]  // alfanumérico, 1º DV errado
    [InlineData("11222333000191")]  // numérico, 1º DV errado
    [InlineData("11222333000182")]  // numérico, 2º DV errado
    [InlineData("12ABC34501DE3A")]  // letra na posição de DV
    [InlineData("12ABC34501DEA5")]  // letra na posição de DV
    [InlineData("00000000000000")]
    [InlineData("AAAAAAAAAAAAAA")]
    [InlineData("1122233300018")]   // 13 caracteres
    [InlineData("112223330001811")] // 15 caracteres
    [InlineData("12ÁBC34501DE35")]  // letra acentuada
    [InlineData("12ÇBC34501DE35")]  // Ç
    [InlineData("12_BC34501DE35")]  // caractere não permitido
    [InlineData("11 222 333 0001 81")]
    [InlineData("")]
    [InlineData(null)]
    public void Criar_FormatoOuDvInvalido_Rejeita(string? entrada)
    {
        var resultado = Cnpj.Criar(entrada);

        Assert.False(resultado.Sucesso);
        Assert.Equal("documento", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public void Criar_MinusculasEMaiusculas_ProduzemOMesmoValor()
    {
        Assert.Equal(Cnpj.Criar("12abc34501de35").Valor, Cnpj.Criar("12ABC34501DE35").Valor);
    }
}
