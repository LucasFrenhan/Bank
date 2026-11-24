using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Cobrança e efeitos do pagamento (specs/014-cartao-credito-cobrancas, FR-009, FR-010, FR-013, FR-018).</summary>
public class CobrancaTests
{
    private static readonly DateTimeOffset Agora = CartoesDeTeste.Agora;

    [Fact]
    public void Emitir_NascePendente()
    {
        var cobranca = CartoesDeTeste.Cobranca();

        Assert.Equal(StatusCobranca.Pendente, cobranca.Status);
        Assert.Equal((299.90m, "Café Especial", Agora), (cobranca.Valor, cobranca.Descricao, cobranca.CriadoEm));
        Assert.Null(cobranca.PagaEm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Descricao_Obrigatoria(string? texto) =>
        Assert.Equal(Cobranca.CampoDescricao, Assert.Single(Cobranca.ValidarDescricao(texto).Erros).Campo);

    [Fact]
    public void Descricao_Ate200_SemEspacosNasPontas()
    {
        Assert.Equal("Café", Cobranca.ValidarDescricao("  Café ").Valor);
        Assert.True(Cobranca.ValidarDescricao(new string('a', 200)).Sucesso);
        Assert.False(Cobranca.ValidarDescricao(new string('a', 201)).Sucesso);
    }

    [Fact]
    public void Cancelar_SoPendente()
    {
        var cobranca = CartoesDeTeste.Cobranca();

        Assert.True(cobranca.Cancelar(Agora.AddMinutes(1)).Sucesso);
        Assert.Equal((StatusCobranca.Cancelada, Agora.AddMinutes(1)), (cobranca.Status, cobranca.CanceladaEm));
        Assert.Equal(ErrosCobranca.NaoPendente, Assert.Single(cobranca.Cancelar(Agora).Erros));
    }

    [Fact]
    public void Cancelar_Paga_Recusa()
    {
        var cartao = CartoesDeTeste.Cartao();
        var cobranca = CartoesDeTeste.Cobranca();
        CartoesDeTeste.Pagar(cobranca, cartao, CartoesDeTeste.Fatura(cartao));

        Assert.Equal(ErrosCobranca.NaoPendente, Assert.Single(cobranca.Cancelar(Agora).Erros));
        Assert.Equal(StatusCobranca.Paga, cobranca.Status);
    }

    [Fact]
    public void Recusa_MantemPendenteComMotivoEInstante()
    {
        var cobranca = CartoesDeTeste.Cobranca();
        var recusa = AutorizacaoCartao.Recusar(cobranca, UsuarioId.Novo(Agora), null, CartoesDeTeste.Numero, MotivoRecusa.CartaoNaoAutorizado, Guid.NewGuid(), Agora);

        cobranca.RegistrarRecusa(recusa);

        Assert.Equal(StatusCobranca.Pendente, cobranca.Status);
        Assert.Equal((MotivoRecusa.CartaoNaoAutorizado, Agora), (cobranca.UltimaRecusaMotivo, cobranca.UltimaRecusaEm));
        Assert.Equal((ResultadoAutorizacao.Recusada, "7899", "001"), (recusa.Resultado, recusa.UltimosDigitos, recusa.CodigoBandeira));
        Assert.Null(recusa.CodigoAutorizacao);
        Assert.Null(recusa.CartaoId);
    }

    [Fact]
    public void Pagar_ProduzTodosOsEfeitos()
    {
        var cartao = CartoesDeTeste.Cartao();
        var fatura = CartoesDeTeste.Fatura(cartao);
        var cobranca = CartoesDeTeste.Cobranca();

        var execucao = CartoesDeTeste.Pagar(cobranca, cartao, fatura);

        Assert.Equal(StatusCobranca.Paga, cobranca.Status);
        Assert.Equal((cartao.UsuarioId, "001", "7899", Agora), (cobranca.PagadorUsuarioId!.Value, cobranca.CodigoBandeira, cobranca.UltimosDigitosCartao, cobranca.PagaEm!.Value));
        Assert.Equal(cobranca.CodigoAutorizacao, execucao.Autorizacao.CodigoAutorizacao);
        Assert.Matches("^[A-Z0-9]{6}$", cobranca.CodigoAutorizacao);
        Assert.Equal(4700.10m, cartao.LimiteDisponivel);
        Assert.Equal(0m, fatura.Total); // só o processamento lança

        Assert.Equal((fatura.Id, cobranca.Id, 299.90m, CartoesDeTeste.Hoje, "Cafeteria Ltda"),
            (execucao.Compra.FaturaId, execucao.Compra.CobrancaId, execucao.Compra.Valor, execucao.Compra.DataCompra, execucao.Compra.Estabelecimento));

        Assert.Equal((299.90m, 0m, 299.90m, SituacaoRecebivel.Pendente, new DateOnly(2027, 1, 7)),
            (execucao.Recebivel.ValorBruto, execucao.Recebivel.Taxa, execucao.Recebivel.ValorLiquido, execucao.Recebivel.Situacao, execucao.Recebivel.DataPrevista));
        Assert.Equal((cobranca.LojistaUsuarioId, cobranca.CarteiraDestinoId), (execucao.Recebivel.LojistaUsuarioId, execucao.Recebivel.CarteiraId));

        Assert.Equal((cobranca.Id, cobranca.LojistaUsuarioId, cartao.UsuarioId, 299.90m, "7899", Agora, "Cafeteria Ltda"),
            (execucao.Evento.CobrancaId, execucao.Evento.LojistaUsuarioId, execucao.Evento.PagadorUsuarioId, execucao.Evento.Valor,
             execucao.Evento.UltimosDigitosCartao, execucao.Evento.OcorridoEm, execucao.Evento.LojistaNome));
    }

    [Fact]
    public void Pagar_SemLimite_NaoAlteraNada()
    {
        var cartao = CartoesDeTeste.Cartao(100m);
        var cobranca = CartoesDeTeste.Cobranca(100.01m);

        var resultado = PagamentoCobranca.Executar(cobranca, cartao, CartoesDeTeste.Fatura(cartao), "Loja", CartoesDeTeste.Hoje, Guid.NewGuid(), Agora);

        Assert.Equal(ErrosCartao.LimiteInsuficiente, Assert.Single(resultado.Erros));
        Assert.Equal((StatusCobranca.Pendente, 100m), (cobranca.Status, cartao.LimiteDisponivel));
    }

    [Fact]
    public void Pagar_CobrancaCancelada_Recusa()
    {
        var cartao = CartoesDeTeste.Cartao();
        var cobranca = CartoesDeTeste.Cobranca();
        cobranca.Cancelar(Agora);

        var resultado = PagamentoCobranca.Executar(cobranca, cartao, CartoesDeTeste.Fatura(cartao), "Loja", CartoesDeTeste.Hoje, Guid.NewGuid(), Agora);

        Assert.Equal(ErrosCobranca.NaoPendente, Assert.Single(resultado.Erros));
        Assert.Equal(5000m, cartao.LimiteDisponivel);
    }
}
