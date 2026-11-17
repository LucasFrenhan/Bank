using System.Reflection;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

public class UsuarioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criar_CriaUsuarioComExatamenteUmaCarteira()
    {
        var usuario = CriarUsuario("  Lucas Silva  ", "529.982.247-25", TipoConta.PessoaFisica, 10m);

        Assert.Equal("Lucas Silva", usuario.NomeCompleto);
        Assert.Equal("lucas@exemplo.com.br", usuario.Email);
        Assert.Equal("52998224725", usuario.Documento);
        Assert.Equal(TipoConta.PessoaFisica, usuario.TipoConta);
        Assert.Equal(Agora, usuario.CriadoEm);
        Assert.Equal(7, usuario.Id.Version);

        var carteira = usuario.Carteira;
        Assert.NotNull(carteira);
        Assert.Equal(usuario.Id, carteira.UsuarioId);
        Assert.Equal(7, carteira.Id.Version);
        Assert.Equal("10.00", carteira.Saldo.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("BRL", carteira.Moeda);
        Assert.Equal(usuario.CriadoEm, carteira.AtualizadoEm);
    }

    [Fact]
    public void Criar_PessoaJuridica_TipoVemDoDocumento()
    {
        var usuario = CriarUsuario("Empresa Exemplo Ltda", "12ABC34501DE35", TipoConta.PessoaJuridica, 0m);

        Assert.Equal(TipoConta.PessoaJuridica, usuario.TipoConta);
        Assert.Equal(0.00m, usuario.Carteira.Saldo);
    }

    [Fact]
    public void Criar_SaldoInicialNegativo_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CriarUsuario("Lucas", "52998224725", TipoConta.PessoaFisica, -0.01m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidarNome_VazioOuSoEspacos_Rejeita(string? nome)
    {
        Assert.Equal("nomeCompleto", Assert.Single(Usuario.ValidarNome(nome).Erros).Campo);
    }

    [Fact]
    public void ValidarNome_Ate150Caracteres_Aceita_E151_Rejeita()
    {
        Assert.True(Usuario.ValidarNome(new string('a', 150)).Sucesso);
        Assert.False(Usuario.ValidarNome(new string('a', 151)).Sucesso);
    }

    [Fact]
    public void TipoConta_NaoTemSetterPublico()
    {
        var propriedade = typeof(Usuario).GetProperty(nameof(Usuario.TipoConta))!;

        Assert.False(propriedade.SetMethod?.IsPublic ?? false);
        Assert.DoesNotContain(
            typeof(Usuario).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            m => m.Name.Contains("TipoConta", StringComparison.Ordinal) && !m.IsSpecialName);
    }

    private static Usuario CriarUsuario(string nome, string documento, TipoConta tipo, decimal saldo) =>
        Usuario.Criar(
            nome,
            Email.Criar("Lucas@Exemplo.com.br").Valor,
            Documento.Criar(documento, tipo).Valor,
            "hash",
            saldo,
            Agora);
}
