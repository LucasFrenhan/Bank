using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

public class EmailTests
{
    [Theory]
    [InlineData("lucas@exemplo.com.br", "lucas@exemplo.com.br")]
    [InlineData("  Lucas@Exemplo.COM.br ", "lucas@exemplo.com.br")]
    public void Criar_EmailValido_Normaliza(string entrada, string esperado)
    {
        var resultado = Email.Criar(entrada);

        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, resultado.Valor.Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("sem-arroba.com")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    public void Criar_EmailInvalido_Rejeita(string? entrada)
    {
        var resultado = Email.Criar(entrada);

        Assert.False(resultado.Sucesso);
        Assert.Equal("email", Assert.Single(resultado.Erros).Campo);
    }

    [Fact]
    public void Criar_ComMaisDe150Caracteres_Rejeita()
    {
        var email = new string('a', 140) + "@exemplo.com"; // 152

        Assert.False(Email.Criar(email).Sucesso);
        Assert.True(Email.Criar(new string('a', 138) + "@exemplo.com").Sucesso); // 150
    }
}
