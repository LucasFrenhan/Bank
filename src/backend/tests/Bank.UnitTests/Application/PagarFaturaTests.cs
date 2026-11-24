using Bank.Application.Comum;
using Bank.Application.Faturas;
using Bank.Domain.Cartoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Consulta e pagamento de faturas (specs/014-cartao-credito-cobrancas, US4 e US5; FR-027 a FR-028a).</summary>
public sealed class PagarFaturaTests : IDisposable
{
    private readonly BancoCartoes _banco = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    private (Usuario Pf, Cartao Cartao) ComFaturaDisponivel(decimal valor = 1000m, decimal saldo = 5000m)
    {
        var (pf, cartao, _) = _banco.CriarPfComCartao(saldo: saldo);
        var compra = _banco.Comprar(cartao, _banco.CriarPj(), valor).Compra;
        _banco.LancamentosFatura.Add(_banco.Faturas[^1].Lancar(compra, _banco.Relogio.Agora));
        _banco.Faturas[^1].Liberar(new DateOnly(2026, 12, 8), _banco.Relogio.Agora);
        Dia(9);
        return (pf, cartao);
    }

    private void Dia(int dia) => _banco.Relogio.Agora = new DateTimeOffset(2026, 12, dia, 12, 0, 0, TimeSpan.Zero);

    private Task<Bank.Domain.Comum.Resultado<DetalheFatura>> PagarAsync(Usuario pf, string competencia = "2026-12", Guid? chave = null) =>
        _banco.PagarFatura().ExecutarAsync(new PagarFaturaComando(pf.Id, competencia, (chave ?? Guid.NewGuid()).ToString()), Ct);

    [Fact]
    public async Task EmDia_DebitaLancaELiberaOLimite()
    {
        var (pf, cartao) = ComFaturaDisponivel();

        var fatura = (await PagarAsync(pf)).Valor;

        Assert.Equal((SituacaoFatura.Paga, 1000m, 0, 0m, 1000m), (fatura.Situacao, fatura.Total, fatura.DiasAtraso, fatura.Juros, fatura.ValorPago));
        Assert.Equal((4000m, 5000m), (pf.Carteira.Saldo, cartao.LimiteDisponivel));
        Assert.Equal((TipoLancamento.PagamentoFatura, 1000m), (_banco.Lancamentos.Single().Tipo, _banco.Lancamentos.Single().Valor));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.faturas.pagamentos" && m.Tags["vencida"] == "False");
    }

    [Fact]
    public async Task Vencida_CobraJuros_ELimiteLiberadoSemOsJuros()
    {
        var (pf, cartao) = ComFaturaDisponivel();
        Dia(20);

        var fatura = (await PagarAsync(pf)).Valor;

        Assert.Equal((10, 50m, 1050m), (fatura.DiasAtraso, fatura.Juros, fatura.ValorPago));
        Assert.Equal((3950m, 5000m), (pf.Carteira.Saldo, cartao.LimiteDisponivel));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.faturas.pagamentos" && m.Tags["vencida"] == "True");
    }

    [Fact]
    public async Task SaldoInsuficiente_NadaGravado()
    {
        var (pf, cartao) = ComFaturaDisponivel(saldo: 999.99m);

        Assert.Equal(ErrosFatura.SaldoInsuficiente, Assert.Single((await PagarAsync(pf)).Erros));
        Assert.Equal((999.99m, 4000m), (pf.Carteira.Saldo, cartao.LimiteDisponivel));
        Assert.Empty(_banco.Lancamentos);
        Assert.Equal(1, _banco.Desfeitas);
    }

    [Fact]
    public async Task Erros_DoContrato()
    {
        var semCartao = _banco.CriarPf();
        var (pf, _) = ComFaturaDisponivel();

        Assert.Equal([Entradas.CampoChave, "competencia"], (await _banco.PagarFatura().ExecutarAsync(new PagarFaturaComando(pf.Id, "x", null), Ct)).Erros.Select(e => e.Campo));
        Assert.Equal(ErrosCartao.NaoEncontrado, Assert.Single((await PagarAsync(semCartao)).Erros));
        Assert.Equal(ErrosFatura.NaoEncontrada, Assert.Single((await PagarAsync(pf, "2026-11")).Erros));
        Assert.Equal(ErrosFatura.NaoDisponivel, Assert.Single((await PagarAsync(pf, "2027-01")).Erros)); // ciclo atual sem compras
        await PagarAsync(pf);
        Assert.Equal(ErrosFatura.NaoDisponivel, Assert.Single((await PagarAsync(pf)).Erros));
    }

    [Fact]
    public async Task MesmaChave_DevolveOOriginal_EOutraCompetenciaDiverge()
    {
        var (pf, _) = ComFaturaDisponivel();
        var chave = Guid.NewGuid();
        await PagarAsync(pf, chave: chave);

        Assert.Equal(SituacaoFatura.Paga, (await PagarAsync(pf, chave: chave)).Valor.Situacao);
        Assert.Equal(ErrosIdempotencia.Divergente, Assert.Single((await PagarAsync(pf, "2027-01", chave)).Erros));
        Assert.Equal(4000m, pf.Carteira.Saldo);
        Assert.Single(_banco.Lancamentos);
    }

    [Fact]
    public async Task FalhaInesperada_Desfaz()
    {
        var (pf, _) = ComFaturaDisponivel();
        _banco.FalharAoBloquear = pf.Carteira.Id.Valor;

        await Assert.ThrowsAsync<InvalidOperationException>(() => PagarAsync(pf));

        Assert.Equal(1, _banco.Desfeitas);
    }

    [Fact]
    public async Task Listar_IncluiOCicloAtual_EObterDetalha()
    {
        var (pf, _) = ComFaturaDisponivel();
        var listar = new ListarFaturas(_banco, _banco, new MonitorDeOpcoes<Bank.Application.Cartoes.CartoesOptions>(_banco.Opcoes), _banco.Calendario, _banco.Relogio);

        var lista = (await listar.ExecutarAsync(new ListarFaturasConsulta(pf.Id, null, null), Ct)).Valor;
        Assert.Equal(["2027-01", "2026-12"], lista.Itens.Select(f => f.Competencia.ToString()));
        Assert.Equal((null, 1000m), (lista.Itens[0].ValorAPagar, lista.Itens[1].ValorAPagar));
        Assert.Equal("pagina", Assert.Single((await listar.ExecutarAsync(new ListarFaturasConsulta(pf.Id, "0", null), Ct)).Erros).Campo);
        Assert.Equal(ErrosCartao.NaoEncontrado, Assert.Single((await listar.ExecutarAsync(new ListarFaturasConsulta(_banco.CriarPf().Id, null, null), Ct)).Erros));

        var obter = _banco.ObterFatura();
        Assert.Single((await obter.ExecutarAsync(new ObterFaturaConsulta(pf.Id, "2026-12"), Ct)).Valor.Lancamentos);
        Assert.Equal(0m, (await obter.ExecutarAsync(new ObterFaturaConsulta(pf.Id, "2027-01"), Ct)).Valor.Total);
        Assert.Equal(ErrosFatura.NaoEncontrada, Assert.Single((await obter.ExecutarAsync(new ObterFaturaConsulta(pf.Id, "2026-10"), Ct)).Erros));
        Assert.Equal("competencia", Assert.Single((await obter.ExecutarAsync(new ObterFaturaConsulta(pf.Id, "x"), Ct)).Erros).Campo);
    }
}
