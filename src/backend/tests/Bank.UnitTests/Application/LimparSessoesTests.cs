using Bank.Application.Autenticacao;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

/// <summary>Limpeza das sessões encerradas (specs/016-sessao-refresh-token, FR-013).</summary>
public sealed class LimparSessoesTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly SessoesFalsas _sessoes = new();
    private readonly MetricasCapturadas _metricas = new();

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task UsaARetencaoEAInatividadeConfiguradas_EMedeQuandoRemove()
    {
        _sessoes.RemocoesSimuladas = 3;

        var removidas = await Criar().ExecutarAsync(CancellationToken.None);

        Assert.Equal(3, removidas);
        Assert.Equal((Agora.AddDays(-7), TimeSpan.FromMinutes(15)), _sessoes.UltimaLimpeza);
        var medicao = Assert.Single(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.sessoes_removidas");
        Assert.Equal(3, medicao.Valor);
    }

    [Fact]
    public async Task NadaARemover_SemMetrica()
    {
        var removidas = await Criar().ExecutarAsync(CancellationToken.None);

        Assert.Equal(0, removidas);
        Assert.Empty(_metricas.Medicoes);
    }

    private LimparSessoes Criar() =>
        new(_sessoes, OpcoesSessao.Monitor(), new RelogioFixo(Agora), _metricas.Metricas, NullLogger<LimparSessoes>.Instance);
}
