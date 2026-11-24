using Bank.Application.Comum;
using Bank.Application.Recebiveis;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Consulta e antecipação de recebíveis (specs/014-cartao-credito-cobrancas, US6; FR-032, FR-033).</summary>
public sealed class RecebiveisTests : IDisposable
{
    private readonly BancoCartoes _banco = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    private (Usuario Pj, Recebivel Recebivel) ComRecebivel(decimal valor = 299.90m)
    {
        var pj = _banco.CriarPj();
        var (_, cartao, _) = _banco.CriarPfComCartao();
        return (pj, _banco.Comprar(cartao, pj, valor).Recebivel);
    }

    private Task<Bank.Domain.Comum.Resultado<Bank.Application.Abstracoes.DadosRecebivel>> AnteciparAsync(Usuario pj, RecebivelId id, Guid? chave = null) =>
        _banco.AnteciparRecebivel().ExecutarAsync(new AnteciparRecebivelComando(pj.Id, id.ToString(), (chave ?? Guid.NewGuid()).ToString()), Ct);

    [Fact]
    public async Task Antecipar_CreditaOLiquidoELanca()
    {
        var (pj, recebivel) = ComRecebivel();

        var resultado = (await AnteciparAsync(pj, recebivel.Id)).Valor;

        Assert.Equal((SituacaoRecebivel.Antecipado, 6.00m, 293.90m), (resultado.Situacao, resultado.Taxa, resultado.ValorLiquido));
        Assert.Equal(293.90m, pj.Carteira.Saldo);
        Assert.Equal((TipoLancamento.AntecipacaoRecebivel, 293.90m), (_banco.Lancamentos.Single().Tipo, _banco.Lancamentos.Single().Valor));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.recebiveis.antecipados");
    }

    [Fact]
    public async Task MesmaChave_UmCredito_EOutroRecebivelDiverge()
    {
        var (pj, recebivel) = ComRecebivel();
        var (_, cartao, _) = _banco.CriarPfComCartao();
        var outro = _banco.Comprar(cartao, pj, 10m).Recebivel;
        var chave = Guid.NewGuid();
        await AnteciparAsync(pj, recebivel.Id, chave);

        Assert.True((await AnteciparAsync(pj, recebivel.Id, chave)).Sucesso);
        Assert.Equal(ErrosIdempotencia.Divergente, Assert.Single((await AnteciparAsync(pj, outro.Id, chave)).Erros));
        Assert.Equal(293.90m, pj.Carteira.Saldo);
        Assert.Single(_banco.Lancamentos);
    }

    [Fact]
    public async Task NaoAntecipavel_DeOutraPj_Inexistente_EInvalido()
    {
        var (pj, recebivel) = ComRecebivel();
        var outra = _banco.CriarPj();

        Assert.Equal([Entradas.CampoChave, "recebivelId"], (await _banco.AnteciparRecebivel().ExecutarAsync(new AnteciparRecebivelComando(pj.Id, "x", null), Ct)).Erros.Select(e => e.Campo));
        Assert.Equal(ErrosRecebivel.NaoEncontrado, Assert.Single((await AnteciparAsync(outra, recebivel.Id)).Erros));
        Assert.Equal(ErrosRecebivel.NaoEncontrado, Assert.Single((await AnteciparAsync(pj, RecebivelId.Novo(CartoesDeTeste.Agora))).Erros));
        _banco.Relogio.Agora = new DateTimeOffset(2027, 1, 7, 3, 0, 0, TimeSpan.Zero); // 00:00 do dia previsto
        Assert.Equal(ErrosRecebivel.NaoAntecipavel, Assert.Single((await AnteciparAsync(pj, recebivel.Id)).Erros));
        Assert.Equal(0m, pj.Carteira.Saldo);
        Assert.Equal(3, _banco.Desfeitas);
    }

    [Fact]
    public async Task FalhaInesperada_Desfaz()
    {
        var (pj, recebivel) = ComRecebivel();
        _banco.FalharAoBloquear = pj.Carteira.Id.Valor;

        await Assert.ThrowsAsync<InvalidOperationException>(() => AnteciparAsync(pj, recebivel.Id));

        Assert.Equal(1, _banco.Desfeitas);
        Assert.Empty(_banco.Lancamentos);
    }

    [Fact]
    public async Task Consultas_SoDoProprioLojista()
    {
        var (pj, recebivel) = ComRecebivel();
        var outra = _banco.CriarPj();
        var (listar, obter) = (new ListarRecebiveis(_banco), new ObterRecebivel(_banco));

        Assert.Equal(1, (await listar.ExecutarAsync(new ListarRecebiveisConsulta(pj.Id, "Pendente", null, null), Ct)).Valor.TotalItens);
        Assert.Equal(0, (await listar.ExecutarAsync(new ListarRecebiveisConsulta(pj.Id, "Liquidado", null, null), Ct)).Valor.TotalItens);
        Assert.Equal(["situacao", "tamanhoPagina"], (await listar.ExecutarAsync(new ListarRecebiveisConsulta(pj.Id, "x", null, "0"), Ct)).Erros.Select(e => e.Campo));
        Assert.True((await obter.ExecutarAsync(new ObterRecebivelConsulta(pj.Id, recebivel.Id.ToString()), Ct)).Sucesso);
        Assert.Equal(ErrosRecebivel.NaoEncontrado, Assert.Single((await obter.ExecutarAsync(new ObterRecebivelConsulta(outra.Id, recebivel.Id.ToString()), Ct)).Erros));
        Assert.Equal("recebivelId", Assert.Single((await obter.ExecutarAsync(new ObterRecebivelConsulta(pj.Id, "x"), Ct)).Erros).Campo);
    }
}
