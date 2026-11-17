using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Compatibilidade documento × tipo de conta (RN-01, RN-03).</summary>
public class DocumentoTests
{
    [Fact]
    public void Criar_CpfParaPessoaFisica_Aceita()
    {
        var documento = Documento.Criar("529.982.247-25", TipoConta.PessoaFisica).Valor;

        Assert.Equal("52998224725", documento.Valor);
        Assert.Equal(TipoConta.PessoaFisica, documento.TipoConta);
    }

    [Fact]
    public void Criar_CnpjAlfanumericoParaPessoaJuridica_Aceita()
    {
        var documento = Documento.Criar("12.abc.345/01de-35", TipoConta.PessoaJuridica).Valor;

        Assert.Equal("12ABC34501DE35", documento.Valor);
        Assert.Equal(TipoConta.PessoaJuridica, documento.TipoConta);
    }

    [Theory]
    [InlineData("52998224725", TipoConta.PessoaJuridica, "exigem CNPJ")]
    [InlineData("12ABC34501DE35", TipoConta.PessoaFisica, "exigem CPF")]
    [InlineData("11222333000181", TipoConta.PessoaFisica, "exigem CPF")]
    public void Criar_DocumentoIncompativelComTipo_ErroNoCampoDocumento(string valor, TipoConta tipo, string trecho)
    {
        var erro = Assert.Single(Documento.Criar(valor, tipo).Erros);

        Assert.Equal("documento", erro.Campo);
        Assert.Contains(trecho, erro.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_Ausente_Obrigatorio(string? valor)
    {
        Assert.Equal("documento", Assert.Single(Documento.Criar(valor, TipoConta.PessoaFisica).Erros).Campo);
    }

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("12ABC34501DE35", true)]
    [InlineData("12345", false)]
    public void ValidarSemTipo_AceitaCpfOuCnpj(string valor, bool valido)
    {
        Assert.Equal(valido, Documento.ValidarSemTipo(valor).Sucesso);
    }
}
