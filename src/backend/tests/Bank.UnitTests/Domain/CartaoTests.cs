using Bank.Domain.Cartoes;

namespace Bank.UnitTests.Domain;

/// <summary>Cartão, limite e autorizador (specs/014-cartao-credito-cobrancas, FR-004 a FR-006, FR-016).</summary>
public class CartaoTests
{
    [Fact]
    public void Emitir_NasceComLimiteLivreENomeEmMaiusculas()
    {
        var cartao = CartoesDeTeste.Cartao();

        Assert.Equal(Bandeira.CodigoVisa, cartao.CodigoBandeira);
        Assert.Equal("LUCAS SILVA", cartao.NomeImpresso);
        Assert.Equal("11/31", cartao.Validade.ToString());
        Assert.Equal((5000.00m, 0m, 5000.00m), (cartao.LimiteTotal, cartao.LimiteUtilizado, cartao.LimiteDisponivel));
        Assert.Equal(CartoesDeTeste.Agora, cartao.CriadoEm);
    }

    [Fact]
    public void ConsumirLimite_EhInclusivo()
    {
        var cartao = CartoesDeTeste.Cartao(100m);

        Assert.True(cartao.ConsumirLimite(60m).Sucesso);
        Assert.True(cartao.ConsumirLimite(40m).Sucesso);
        Assert.Equal(0m, cartao.LimiteDisponivel);
    }

    [Fact]
    public void ConsumirLimite_AcimaDoDisponivel_RecusaSemAlterar()
    {
        var cartao = CartoesDeTeste.Cartao(100m);

        var resultado = cartao.ConsumirLimite(100.01m);

        Assert.Equal(ErrosCartao.LimiteInsuficiente, Assert.Single(resultado.Erros));
        Assert.Equal(100m, cartao.LimiteDisponivel);
    }

    [Fact]
    public void LiberarLimite_DevolveENuncaFicaNegativo()
    {
        var cartao = CartoesDeTeste.Cartao(100m);
        cartao.ConsumirLimite(60m);

        cartao.LiberarLimite(50m);

        Assert.Equal(90m, cartao.LimiteDisponivel);
        Assert.Throws<InvalidOperationException>(() => cartao.LiberarLimite(10.01m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValoresNaoPositivos_SaoErroDeProgramacao(int valor)
    {
        var cartao = CartoesDeTeste.Cartao();

        Assert.Throws<ArgumentOutOfRangeException>(() => cartao.ConsumirLimite(valor));
        Assert.Throws<ArgumentOutOfRangeException>(() => cartao.LiberarLimite(valor));
    }

    [Theory]
    [InlineData(false, true, 0, 10, MotivoRecusa.CartaoNaoAutorizado)]
    [InlineData(true, true, 0, 10, MotivoRecusa.FaturaVencida)]
    [InlineData(true, false, 9, 10, MotivoRecusa.LimiteInsuficiente)]
    [InlineData(true, false, 10, 10, null)]
    public void Autorizador_DecideNaOrdemDoContrato(bool confere, bool vencida, int limite, int valor, MotivoRecusa? esperado) =>
        Assert.Equal(esperado, AutorizadorBandeira.Decidir(confere, vencida, limite, valor));

    [Theory]
    [InlineData(MotivoRecusa.CartaoNaoAutorizado, "cartao-nao-autorizado")]
    [InlineData(MotivoRecusa.FaturaVencida, "cartao-bloqueado-fatura-vencida")]
    [InlineData(MotivoRecusa.LimiteInsuficiente, "limite-cartao-insuficiente")]
    public void Autorizador_ErroDeCadaMotivo(MotivoRecusa motivo, string codigo) =>
        Assert.Equal(codigo, AutorizadorBandeira.ErroDe(motivo).Codigo);
}
