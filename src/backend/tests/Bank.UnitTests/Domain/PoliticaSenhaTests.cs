using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Política: 8–128 caracteres; maiúscula, minúscula, dígito e caractere especial (FR-009).</summary>
public class PoliticaSenhaTests
{
    [Theory]
    [InlineData("SenhaForte@123")]
    [InlineData("Aa1!aaaa")]
    public void Validar_SenhaQueAtendeAPolitica_SemErros(string senha)
    {
        Assert.Empty(PoliticaSenha.Validar(senha));
    }

    [Fact]
    public void Validar_Com128Caracteres_Aceita_E129_Rejeita()
    {
        Assert.Empty(PoliticaSenha.Validar("Aa1!" + new string('x', 124)));
        Assert.Single(PoliticaSenha.Validar("Aa1!" + new string('x', 125)));
    }

    [Theory]
    [InlineData("Aa1!aaa", "entre 8 e 128")]
    [InlineData("senhafraca@1", "maiúscula")]
    [InlineData("SENHAFRACA@1", "minúscula")]
    [InlineData("SenhaFraca@", "dígito")]
    [InlineData("SenhaFraca1", "especial")]
    public void Validar_UmaRegraViolada_UmErroComMensagemPropria(string senha, string trecho)
    {
        var erro = Assert.Single(PoliticaSenha.Validar(senha));

        Assert.Equal("senha", erro.Campo);
        Assert.Contains(trecho, erro.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Validar_VariasRegrasVioladas_UmErroPorRegra()
    {
        Assert.Equal(4, PoliticaSenha.Validar("abc").Count); // tamanho, maiúscula, dígito, especial
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validar_Ausente_Obrigatoria(string? senha)
    {
        Assert.Contains("obrigatória", Assert.Single(PoliticaSenha.Validar(senha)).Mensagem, StringComparison.Ordinal);
    }
}
