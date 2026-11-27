using Bank.Application.Autenticacao;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.UnitTests.Application;

/// <summary>Renovação da sessão (specs/016-sessao-refresh-token, US1, US3; FR-006 a FR-010, FR-021).</summary>
public sealed class RenovarSessaoTests : IDisposable
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly RepositorioFalso _usuarios = new();
    private readonly SessoesFalsas _sessoes = new();
    private readonly GeradorFalso _gerador = new();
    private readonly UnidadeFalsa _unidade = new();
    private readonly RelogioManual _relogio = new(Inicio);
    private readonly MetricasCapturadas _metricas = new();
    private readonly LogsCapturados<RenovarSessao> _logs = new();
    private readonly RenovarSessao _casoDeUso;
    private readonly Usuario _usuario;
    private readonly Sessao _sessao;

    public RenovarSessaoTests()
    {
        _casoDeUso = new RenovarSessao(
            _sessoes, _usuarios, _gerador, new EmissorFalso(), _unidade, OpcoesSessao.Monitor(), _relogio, _metricas.Metricas, _logs);
        _usuario = Usuario.Criar(
            "Lucas Silva",
            Email.Criar("lucas@exemplo.com.br").Valor,
            Documento.Criar("52998224725", TipoConta.PessoaFisica).Valor,
            "hash",
            ContasDeTeste.Corrente(),
            10m,
            Inicio);
        _usuarios.Usuarios.Add(_usuario);
        _sessao = Sessao.Iniciar(_usuario.Id, GeradorFalso.Hash("inicial"), Inicio, OpcoesSessao.Padrao().Limites());
        _sessoes.Sessoes.Add(_sessao);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task CredencialAtiva_EmiteTokenComSessaoECredencialNova()
    {
        _relogio.Avancar(TimeSpan.FromMinutes(5));

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("token-de:" + _usuario.Id + ":" + _sessao.Id, resultado.Valor.Token);
        Assert.Equal("cred-1", resultado.Valor.Credencial);
        Assert.Equal(GeradorFalso.Hash("cred-1"), _sessao.CredencialAtiva.Hash);
        Assert.Equal(900, resultado.Valor.SessaoExpiraPorInatividadeEmSegundos);
        Assert.Equal(28800 - 300, resultado.Valor.SessaoExpiraEmSegundos);
        Assert.Equal((1, 1, 0), (_unidade.Inicios, _unidade.Confirmacoes, _unidade.Desfeitas));
        AssertRenovacao("sucesso");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inventada")]
    public async Task CredencialAusenteOuDesconhecida_NaoAutenticado(string? credencial)
    {
        var resultado = await _casoDeUso.ExecutarAsync(credencial, CancellationToken.None);

        Assert.Equal(ErrosSessao.NaoAutenticado, Assert.Single(resultado.Erros));
        Assert.Equal(0, _unidade.Confirmacoes);
        AssertRenovacao("recusada");
    }

    [Fact]
    public async Task SessaoRevogada_NaoAutenticadoSemGravar()
    {
        _sessao.Revogar(MotivoRevogacao.Logout, Inicio);

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.Equal(ErrosSessao.NaoAutenticado, Assert.Single(resultado.Erros));
        Assert.Equal((0, 1), (_unidade.Confirmacoes, _unidade.Desfeitas));
        AssertRenovacao("recusada");
        Assert.Contains(_logs.Mensagens, m => m.Nivel == LogLevel.Information && m.Texto.Contains("Revogada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SessaoInativa_NaoAutenticado()
    {
        _relogio.Avancar(TimeSpan.FromMinutes(15));

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.Equal(ErrosSessao.NaoAutenticado, Assert.Single(resultado.Erros));
        Assert.False(_sessao.Revogada);
    }

    [Fact]
    public async Task UsuarioInexistente_NaoAutenticado()
    {
        _usuarios.Usuarios.Clear();

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.Equal(ErrosSessao.NaoAutenticado, Assert.Single(resultado.Erros));
        Assert.Equal(0, _unidade.Confirmacoes);
    }

    [Fact]
    public async Task Reuso_MesmoErro_GravaRevogacao_LogWarningSemSegredos_EMetricas()
    {
        _relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.True((await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None)).Sucesso);
        _relogio.Avancar(TimeSpan.FromSeconds(31));

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.Equal(ErrosSessao.NaoAutenticado, Assert.Single(resultado.Erros));
        Assert.Equal(MotivoRevogacao.Reuso, _sessao.MotivoRevogacao);
        Assert.Equal(2, _unidade.Confirmacoes); // a revogação é gravada
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.renovacoes" && m.Tags["resultado"] == "reuso");
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.sessoes_revogadas" && m.Tags["motivo"] == "reuso");

        var aviso = Assert.Single(_logs.Mensagens, m => m.Nivel == LogLevel.Warning);
        Assert.Contains(_sessao.Id.ToString(), aviso.Texto, StringComparison.Ordinal);
        Assert.Contains(_usuario.Id.ToString(), aviso.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("inicial", aviso.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("cred-", aviso.Texto, StringComparison.Ordinal);

        // A credencial mais nova também foi derrubada.
        Assert.False((await _casoDeUso.ExecutarAsync("cred-1", CancellationToken.None)).Sucesso);
    }

    [Fact]
    public async Task DentroDaTolerancia_RenovaDeNovoSemRevogar()
    {
        _relogio.Avancar(TimeSpan.FromMinutes(1));
        await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromSeconds(10));

        var resultado = await _casoDeUso.ExecutarAsync("inicial", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("cred-2", resultado.Valor.Credencial);
        Assert.False(_sessao.Revogada);
    }

    private void AssertRenovacao(string resultado)
    {
        var medicao = Assert.Single(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.renovacoes");
        Assert.Equal(resultado, medicao.Tags["resultado"]);
    }
}

/// <summary>Logger que guarda nível e texto formatado.</summary>
internal sealed class LogsCapturados<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Texto)> Mensagens { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Mensagens.Add((logLevel, formatter(state, exception)));
    }
}
