using Bank.Application.Processamento;
using Bank.Domain.Cartoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Microsoft.Extensions.Logging;

namespace Bank.UnitTests.Application;

/// <summary>Processamento diário (specs/014-cartao-credito-cobrancas, FR-022, FR-023, FR-031; research R7).</summary>
public sealed class ProcessarDiaTests : IDisposable
{
    private readonly BancoCartoes _banco = new();
    private readonly LoggerCapturado<ProcessarDia> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    /// <summary>Executa às 00:00 de Brasília do dia.</summary>
    private Task<ResumoProcessamento> ProcessarAsync(int ano, int mes, int dia)
    {
        _banco.Relogio.Agora = new DateTimeOffset(ano, mes, dia, 3, 0, 0, TimeSpan.Zero);
        return _banco.ProcessarDia(_logger).ExecutarAsync(Ct);
    }

    [Fact]
    public async Task SemTrabalho_OuDiaNaoUtil_NadaAcontece_SemMetricaNemLog()
    {
        Assert.Equal(ResumoProcessamento.Vazio, await ProcessarAsync(2026, 11, 25));
        var (_, cartao, _) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, _banco.CriarPj(), 10m); // 25/11

        Assert.Equal(ResumoProcessamento.Vazio, await ProcessarAsync(2026, 11, 28)); // sábado
        Assert.Equal(ResumoProcessamento.Vazio, await ProcessarAsync(2026, 12, 25)); // feriado

        Assert.Empty(_logger.Registros);
        Assert.Empty(_banco.Medicoes);
        Assert.Empty(_banco.LancamentosFatura);
    }

    [Fact]
    public async Task LancaSoAsDeDiasAnteriores_ENaoRepete()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, _) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, pj, 10m); // 24/11
        _banco.Relogio.Agora = new DateTimeOffset(2026, 11, 25, 3, 0, 0, TimeSpan.Zero);
        _banco.Comprar(cartao, pj, 5m); // 25/11

        Assert.Equal(new ResumoProcessamento(1, 0, 0, 0), await ProcessarAsync(2026, 11, 25));
        Assert.Equal(ResumoProcessamento.Vazio, await ProcessarAsync(2026, 11, 25));
        Assert.Equal(new ResumoProcessamento(1, 0, 0, 0), await ProcessarAsync(2026, 11, 26));

        Assert.Equal((15m, 2), (_banco.Faturas.Single().Total, _banco.LancamentosFatura.Count));
        Assert.Null(_banco.Faturas.Single().LiberadaEm);
    }

    [Fact]
    public async Task FaturaFechada_LancaELibera_ERecebivelDevidoEhLiquidado()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, _) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, pj, 10m);

        Assert.Equal(new ResumoProcessamento(1, 1, 0, 0), await ProcessarAsync(2026, 12, 8));
        Assert.Equal(SituacaoFatura.Disponivel, _banco.Faturas.Single().Situacao(new DateOnly(2026, 12, 8)));

        Assert.Equal(new ResumoProcessamento(0, 0, 1, 0), await ProcessarAsync(2027, 1, 7));
        Assert.Equal((SituacaoRecebivel.Liquidado, 10m), (_banco.Recebiveis.Single().Situacao, pj.Carteira.Saldo));
        Assert.Equal((TipoLancamento.LiquidacaoRecebivel, 10m), (_banco.Lancamentos.Single().Tipo, _banco.Lancamentos.Single().Valor));
        Assert.Equal(ResumoProcessamento.Vazio, await ProcessarAsync(2027, 1, 8));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.processamento.recebiveis_liquidados");
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.processamento.faturas_liberadas" && m.Valor == 1);
    }

    [Fact]
    public async Task FalhaEmUmItem_NaoParaOsDemais_EContaPorEtapa()
    {
        var pj = _banco.CriarPj();
        var (_, comFalha, _) = _banco.CriarPfComCartao();
        var (_, semFalha, _) = _banco.CriarPfComCartao();
        _banco.Comprar(comFalha, pj, 10m);
        _banco.Comprar(semFalha, pj, 20m);
        _banco.FalharAoBloquear = comFalha.Id.Valor;

        Assert.Equal(new ResumoProcessamento(1, 1, 0, 1), await ProcessarAsync(2026, 12, 8));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.processamento.falhas" && m.Tags["etapa"] == "lancamento");
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.processamento.execucoes" && m.Tags["resultado"] == "com-falhas");
        Assert.Contains(_logger.Registros, r => r.Nivel == LogLevel.Error);

        _banco.FalharAoBloquear = null;
        Assert.Equal(new ResumoProcessamento(1, 1, 0, 0), await ProcessarAsync(2026, 12, 8));
    }

    [Fact]
    public async Task FalhaNaLiquidacao_EAntecipadoDuranteARodada()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, _) = _banco.CriarPfComCartao();
        var (a, b) = (_banco.Comprar(cartao, pj, 10m).Recebivel, _banco.Comprar(cartao, pj, 20m).Recebivel);
        await ProcessarAsync(2026, 12, 8);
        _banco.FalharAoBloquear = a.Id.Valor;
        _banco.AoBloquear = () =>
        {
            // Outra requisição antecipou o segundo enquanto a rodada esperava o bloqueio.
            _banco.AoBloquear = null;
            b.Antecipar(CartoesDeTeste.Hoje, Guid.NewGuid(), CartoesDeTeste.Agora);
        };

        Assert.Equal(new ResumoProcessamento(0, 0, 0, 1), await ProcessarAsync(2027, 1, 7));

        Assert.Equal(0m, pj.Carteira.Saldo);
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.processamento.falhas" && m.Tags["etapa"] == "liquidacao");
    }

    [Fact]
    public async Task Cancelamento_InterrompeSemContarFalha()
    {
        var (_, cartao, _) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, _banco.CriarPj(), 10m);
        using var cancelamento = new CancellationTokenSource();
        _banco.AoBloquear = () =>
        {
            cancelamento.Cancel();
            cancelamento.Token.ThrowIfCancellationRequested();
        };
        _banco.Relogio.Agora = new DateTimeOffset(2026, 12, 8, 3, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _banco.ProcessarDia(_logger).ExecutarAsync(cancelamento.Token));

        Assert.Empty(_banco.LancamentosFatura);
    }
}
