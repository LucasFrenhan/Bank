using Bank.Domain.Permissoes;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Matriz RN-06 (FR-023).</summary>
public class PermissoesPorPerfilTests
{
    [Theory]
    [InlineData(TipoConta.PessoaFisica, Permissao.RealizarTransferencia, true)]
    [InlineData(TipoConta.PessoaFisica, Permissao.ReceberTransferencia, true)]
    [InlineData(TipoConta.PessoaFisica, Permissao.PagarCobrancaCartao, true)]
    [InlineData(TipoConta.PessoaFisica, Permissao.GerarCobrancaCartao, false)]
    [InlineData(TipoConta.PessoaJuridica, Permissao.RealizarTransferencia, false)]
    [InlineData(TipoConta.PessoaJuridica, Permissao.ReceberTransferencia, true)]
    [InlineData(TipoConta.PessoaJuridica, Permissao.PagarCobrancaCartao, false)]
    [InlineData(TipoConta.PessoaJuridica, Permissao.GerarCobrancaCartao, true)]
    public void Possui_SegueAMatrizDaRn06(TipoConta tipo, Permissao permissao, bool esperado)
    {
        Assert.Equal(esperado, PermissoesPorPerfil.Possui(tipo, permissao));
    }

    [Fact]
    public void Do_TipoDesconhecido_SemPermissoes()
    {
        Assert.Empty(PermissoesPorPerfil.Do((TipoConta)99));
        Assert.False(PermissoesPorPerfil.Possui((TipoConta)99, Permissao.ReceberTransferencia));
    }
}
