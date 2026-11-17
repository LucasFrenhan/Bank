using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

public class TipoContaTests
{
    [Theory]
    [InlineData("PessoaFisica", TipoConta.PessoaFisica)]
    [InlineData("PessoaJuridica", TipoConta.PessoaJuridica)]
    public void Converter_NomeExato_Aceita(string entrada, TipoConta esperado)
    {
        Assert.Equal(esperado, TiposConta.Converter(entrada).Valor);
    }

    [Theory]
    [InlineData("pessoafisica")]
    [InlineData("PESSOAJURIDICA")]
    [InlineData("PF")]
    [InlineData("PJ")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void Converter_ValorDesconhecido_ErroNoCampoTipoConta(string? entrada)
    {
        var resultado = TiposConta.Converter(entrada);

        Assert.False(resultado.Sucesso);
        Assert.Equal("tipoConta", Assert.Single(resultado.Erros).Campo);
    }
}
