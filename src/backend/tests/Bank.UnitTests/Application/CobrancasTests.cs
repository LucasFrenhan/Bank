using Bank.Application.Cobrancas;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Emissão, visibilidade, lista e cancelamento (specs/014-cartao-credito-cobrancas, US2).</summary>
public sealed class CobrancasTests : IDisposable
{
    private readonly BancoCartoes _banco = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    private EmitirCobranca Emitir() => new(_banco, _banco, _banco, _banco, _banco.Relogio, _banco.MetricasCartoes, new LoggerCapturado<EmitirCobranca>());

    private CancelarCobranca Cancelar() => new(_banco, _banco, _banco, _banco.Relogio, _banco.MetricasCartoes, new LoggerCapturado<CancelarCobranca>());

    [Fact]
    public async Task Emitir_GravaNaCarteiraDoLojista()
    {
        var pj = _banco.CriarPj();

        var resultado = await Emitir().ExecutarAsync(new EmitirCobrancaComando(pj.Id, 10m, " Café "), Ct);

        var cobranca = Assert.Single(_banco.Cobrancas);
        Assert.Equal((pj.Carteira.Id, 10m, "Café"), (cobranca.CarteiraDestinoId, cobranca.Valor, cobranca.Descricao));
        Assert.Equal(cobranca.Id, resultado.Valor.Dados.CobrancaId);
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.cobrancas.emitidas");
    }

    [Fact]
    public async Task Emitir_Invalida_TodosOsErros_EUsuarioInexistente()
    {
        Assert.Equal(["valor", "descricao"], (await Emitir().ExecutarAsync(new EmitirCobrancaComando(_banco.CriarPj().Id, 0m, ""), Ct)).Erros.Select(e => e.Campo));
        Assert.Equal(ErrosUsuario.NaoEncontrado, Assert.Single((await Emitir().ExecutarAsync(new EmitirCobrancaComando(UsuarioId.Novo(CartoesDeTeste.Agora), 1m, "x"), Ct)).Erros));
        Assert.Empty(_banco.Cobrancas);
    }

    [Fact]
    public async Task Consultar_PagamentoERecusaConformeQuemVe()
    {
        var pj = _banco.CriarPj();
        var (pf, cartao, _) = _banco.CriarPfComCartao();
        var terceiro = _banco.CriarPf();
        var pagamento = _banco.Comprar(cartao, pj, 10m);
        var recusada = _banco.CriarCobranca(pj);
        recusada.RegistrarRecusa(AutorizacaoCartao.Recusar(recusada, pf.Id, null, CartoesDeTeste.Numero, MotivoRecusa.LimiteInsuficiente, Guid.NewGuid(), CartoesDeTeste.Agora));

        Assert.NotNull((await ConsultarAsync(pj.Id, pagamento.Compra.CobrancaId)).Pagamento);
        Assert.NotNull((await ConsultarAsync(pf.Id, pagamento.Compra.CobrancaId)).Pagamento);
        Assert.Null((await ConsultarAsync(terceiro.Id, pagamento.Compra.CobrancaId)).Pagamento);
        Assert.Equal(MotivoRecusa.LimiteInsuficiente, (await ConsultarAsync(pj.Id, recusada.Id)).UltimaRecusa!.Motivo);
        Assert.Null((await ConsultarAsync(pf.Id, recusada.Id)).UltimaRecusa);
        Assert.Null((await ConsultarAsync(pj.Id, recusada.Id)).Pagamento);

        async Task<DetalheCobranca> ConsultarAsync(UsuarioId quem, CobrancaId id) =>
            (await new ConsultarCobranca(_banco).ExecutarAsync(new ConsultarCobrancaConsulta(quem, id.ToString()), Ct)).Valor;
    }

    [Fact]
    public async Task Consultar_InvalidaOuInexistente()
    {
        var casoDeUso = new ConsultarCobranca(_banco);
        var quem = _banco.CriarPf().Id;

        Assert.Equal(ConsultarCobranca.CampoIdentificador, Assert.Single((await casoDeUso.ExecutarAsync(new ConsultarCobrancaConsulta(quem, "x"), Ct)).Erros).Campo);
        Assert.Equal(ErrosCobranca.NaoEncontrada, Assert.Single((await casoDeUso.ExecutarAsync(new ConsultarCobrancaConsulta(quem, Guid.NewGuid().ToString()), Ct)).Erros));
    }

    [Fact]
    public async Task Listar_FiltroEPagina()
    {
        var pj = _banco.CriarPj();
        _banco.CriarCobranca(pj);
        _banco.CriarCobranca(pj).Cancelar(CartoesDeTeste.Agora);
        _banco.CriarCobranca(_banco.CriarPj());
        var casoDeUso = new ListarMinhasCobrancas(_banco);

        var todas = (await casoDeUso.ExecutarAsync(new ListarMinhasCobrancasConsulta(pj.Id, null, null, null), Ct)).Valor;
        Assert.Equal((2, 1, 20), (todas.TotalItens, todas.PaginaAtual, todas.TamanhoPagina));
        Assert.Single((await casoDeUso.ExecutarAsync(new ListarMinhasCobrancasConsulta(pj.Id, "Cancelada", "1", "5"), Ct)).Valor.Itens);
        Assert.Equal(["status", "pagina"], (await casoDeUso.ExecutarAsync(new ListarMinhasCobrancasConsulta(pj.Id, "x", "0", null), Ct)).Erros.Select(e => e.Campo));
    }

    [Fact]
    public async Task Cancelar_SoAPropriaPendente()
    {
        var (pj, outra) = (_banco.CriarPj(), _banco.CriarPj());
        var cobranca = _banco.CriarCobranca(pj);

        Assert.Equal("cobrancaId", Assert.Single((await Cancelar().ExecutarAsync(new CancelarCobrancaComando(pj.Id, "x"), Ct)).Erros).Campo);
        Assert.Equal(ErrosCobranca.NaoEncontrada, Assert.Single((await Cancelar().ExecutarAsync(new CancelarCobrancaComando(outra.Id, cobranca.Id.ToString()), Ct)).Erros));
        Assert.Equal(StatusCobranca.Pendente, cobranca.Status);

        Assert.Equal(StatusCobranca.Cancelada, (await Cancelar().ExecutarAsync(new CancelarCobrancaComando(pj.Id, cobranca.Id.ToString()), Ct)).Valor.Dados.Status);
        Assert.Equal(ErrosCobranca.NaoPendente, Assert.Single((await Cancelar().ExecutarAsync(new CancelarCobrancaComando(pj.Id, cobranca.Id.ToString()), Ct)).Erros));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.cobrancas.canceladas");
    }

    [Fact]
    public async Task Falha_DesfazATransacao()
    {
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        _banco.FalharAoBloquear = cobranca.Id.Valor;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Cancelar().ExecutarAsync(new CancelarCobrancaComando(cobranca.LojistaUsuarioId, cobranca.Id.ToString()), Ct));

        Assert.Equal(1, _banco.Desfeitas);
    }
}
