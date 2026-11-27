using Bank.Application.Autenticacao;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

/// <summary>"Sair" no servidor (specs/016-sessao-refresh-token, US2; FR-011).</summary>
public sealed class EncerrarSessaoTests : IDisposable
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitesSessao Limites = OpcoesSessao.Padrao().Limites();

    private readonly SessoesFalsas _sessoes = new();
    private readonly UnidadeFalsa _unidade = new();
    private readonly RelogioManual _relogio = new(Inicio.AddMinutes(10));
    private readonly MetricasCapturadas _metricas = new();
    private readonly EncerrarSessao _casoDeUso;
    private readonly Sessao _sessao;

    public EncerrarSessaoTests()
    {
        _casoDeUso = new EncerrarSessao(_sessoes, new GeradorFalso(), _unidade, _relogio, _metricas.Metricas, NullLogger<EncerrarSessao>.Instance);
        _sessao = Sessao.Iniciar(UsuarioId.Novo(Inicio), GeradorFalso.Hash("a"), Inicio, Limites);
        _sessoes.Sessoes.Add(_sessao);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task CredencialAtiva_RevogaComLogoutEMetrica()
    {
        await _casoDeUso.ExecutarAsync("a", CancellationToken.None);

        Assert.Equal(MotivoRevogacao.Logout, _sessao.MotivoRevogacao);
        Assert.Equal(_relogio.Agora, _sessao.RevogadaEm);
        Assert.Equal(1, _unidade.Confirmacoes);
        var medicao = Assert.Single(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.sessoes_revogadas");
        Assert.Equal("logout", medicao.Tags["motivo"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    public async Task CredencialJaTrocada_DentroOuForaDaTolerancia_TambemRevoga(int segundosDepois)
    {
        _sessao.Renovar(GeradorFalso.Hash("a"), GeradorFalso.Hash("b"), Inicio.AddMinutes(9), Limites);
        _relogio.Agora = Inicio.AddMinutes(9).AddSeconds(segundosDepois);

        await _casoDeUso.ExecutarAsync("a", CancellationToken.None);

        Assert.Equal(MotivoRevogacao.Logout, _sessao.MotivoRevogacao);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inventada")]
    public async Task AusenteOuDesconhecida_SemEfeito(string? credencial)
    {
        await _casoDeUso.ExecutarAsync(credencial, CancellationToken.None);

        Assert.False(_sessao.Revogada);
        Assert.Equal(0, _unidade.Confirmacoes);
        Assert.Empty(_metricas.Medicoes);
    }

    [Fact]
    public async Task SessaoJaRevogada_SemEfeito()
    {
        _sessao.Revogar(MotivoRevogacao.Reuso, Inicio);

        await _casoDeUso.ExecutarAsync("a", CancellationToken.None);

        Assert.Equal(MotivoRevogacao.Reuso, _sessao.MotivoRevogacao);
        Assert.Equal(0, _unidade.Confirmacoes);
        Assert.Empty(_metricas.Medicoes);
    }
}
