using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Lançamento de abertura no cadastro (spec 006, FR-016) e documento de terceiros no extrato (FR-014).</summary>
public class LancamentoAberturaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UsuarioCriar_ComSaldo_GeraAberturaDeEntrada()
    {
        var usuario = Usuario.Criar("Lucas", Email.Criar("a@b.com.br").Valor, Documento.Criar("52998224725", TipoConta.PessoaFisica).Valor, "hash", 10m, Agora);

        var abertura = usuario.LancamentoDeAbertura!;
        Assert.Equal((usuario.Carteira.Id, TipoLancamento.Abertura, DirecaoLancamento.Entrada, 10m, null as TransferenciaId?), (abertura.CarteiraId, abertura.Tipo, abertura.Direcao, abertura.Valor, abertura.TransferenciaId));
        Assert.Equal("Saldo inicial de demonstração", abertura.Descricao);
        Assert.Equal(Agora, abertura.CriadoEm);
    }

    [Fact]
    public void UsuarioCriar_SemSaldo_NaoGeraAbertura()
    {
        var usuario = Usuario.Criar("Empresa", Email.Criar("e@b.com.br").Valor, Documento.Criar("12ABC34501DE35", TipoConta.PessoaJuridica).Valor, "hash", 0m, Agora);

        Assert.Null(usuario.LancamentoDeAbertura);
    }

    [Theory]
    [InlineData("52998224725", "•••.982.247-••")]
    [InlineData("12ABC34501DE35", "12ABC34501DE35")]
    public void Ocultar_MascaraCpfEMantemCnpj(string documento, string esperado)
    {
        Assert.Equal(esperado, Documento.Ocultar(documento));
    }

    [Theory]
    [InlineData("529.982.247-25", "52998224725", TipoConta.PessoaFisica)]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35", TipoConta.PessoaJuridica)]
    public void CriarSemTipo_NormalizaCpfECnpj(string entrada, string valor, TipoConta tipo)
    {
        var documento = Documento.CriarSemTipo(entrada).Valor;

        Assert.Equal((valor, tipo), (documento.Valor, documento.TipoConta));
    }

    [Fact]
    public void CriarSemTipo_Invalido_ErroNoCampoInformado()
    {
        Assert.Equal("documentoDestino", Assert.Single(Documento.CriarSemTipo("123", "documentoDestino").Erros).Campo);
    }
}
