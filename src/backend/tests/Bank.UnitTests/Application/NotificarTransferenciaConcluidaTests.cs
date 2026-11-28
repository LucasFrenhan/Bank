using Bank.Application.Abstracoes;
using Bank.Application.Notificacoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Notificacoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.UnitTests.Application;

/// <summary>
/// Notificação de transferência: log e métrica (specs/010-mensageria-rabbitmq, FR-016, FR-023;
/// research R8) e as duas notificações persistidas (specs/011-notificacoes-auditoria, FR-001,
/// FR-004, FR-010; research R3, R6).
/// </summary>
public sealed class NotificarTransferenciaConcluidaTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly MetricasCapturadasBase _metricas = new(f => new MetricasNotificacoes(f));
    private readonly LoggerCapturado<NotificarTransferenciaConcluida> _logger = new();
    private readonly NotificacoesAuditoriaEmMemoria _notificacoes = new();

    public void Dispose() => _metricas.Dispose();

    private NotificarTransferenciaConcluida CasoDeUso() =>
        new((MetricasNotificacoes)_metricas.Instancia, _notificacoes, new RelogioFixo(Agora), _logger);

    private static NotificacaoTransferencia Notificacao(string? pagadorNome = "João Silva", string? recebedorNome = "Maria Souza") => new(
        EventoId.De(Guid.CreateVersion7()),
        TransferenciaId.De(Guid.CreateVersion7()),
        UsuarioId.De(Guid.CreateVersion7()),
        UsuarioId.De(Guid.CreateVersion7()),
        12.34m,
        DateTimeOffset.UtcNow,
        pagadorNome,
        recebedorNome);

    [Fact]
    public async Task Notificar_RegistraUmLogEUmaMetricaPorDestinatario()
    {
        var notificacao = Notificacao();

        await CasoDeUso().NotificarAsync(notificacao, TestContext.Current.CancellationToken);

        Assert.Equal(2, _logger.Registros.Count);
        var pagador = _logger.Registros.Single(r => Equals(r.Propriedades["Papel"], "pagador"));
        var recebedor = _logger.Registros.Single(r => Equals(r.Propriedades["Papel"], "recebedor"));
        Assert.Equal(notificacao.PagadorUsuarioId.Valor, pagador.Propriedades["UsuarioId"]);
        Assert.Equal(notificacao.RecebedorUsuarioId.Valor, recebedor.Propriedades["UsuarioId"]);
        Assert.All(_logger.Registros, r =>
        {
            Assert.Equal(notificacao.EventoId.Valor, r.Propriedades["EventoId"]);
            Assert.Equal(notificacao.TransferenciaId.Valor, r.Propriedades["TransferenciaId"]);
            Assert.Equal(12.34m, r.Propriedades["Valor"]);
        });

        Assert.Equal(
            ["pagador", "recebedor"],
            _metricas.Medicoes.Where(m => m.Nome == "bank.notificacoes.enviadas").Select(m => m.Tags["papel"]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Notificar_GravaComprovanteParaOPagadorEAvisoParaORecebedor()
    {
        var notificacao = Notificacao();

        await CasoDeUso().NotificarAsync(notificacao, TestContext.Current.CancellationToken);

        Assert.Equal(2, _notificacoes.Gravadas.Count);
        var comprovante = _notificacoes.Gravadas.Single(n => n.Tipo == TipoNotificacao.ComprovanteEnvio);
        var aviso = _notificacoes.Gravadas.Single(n => n.Tipo == TipoNotificacao.AvisoCredito);
        Assert.Equal(notificacao.PagadorUsuarioId, comprovante.UsuarioDestinoId);
        Assert.Equal(notificacao.RecebedorUsuarioId, aviso.UsuarioDestinoId);
        Assert.Equal("Você enviou R$ 12,34 para Maria Souza.", comprovante.Mensagem);
        Assert.Equal("Você recebeu R$ 12,34 de João Silva.", aviso.Mensagem);
        Assert.All(_notificacoes.Gravadas, n =>
        {
            Assert.Equal(notificacao.EventoId, n.EventoId);
            Assert.Equal(Agora, n.CriadoEm);
            Assert.Equal(CanalNotificacao.Simulado, n.Canal);
        });
    }

    [Fact]
    public async Task Notificar_SemNomes_TextosSoComOValor()
    {
        await CasoDeUso().NotificarAsync(Notificacao(pagadorNome: null, recebedorNome: null), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Você enviou R$ 12,34.", "Você recebeu R$ 12,34."],
            _notificacoes.Gravadas.Select(n => n.Mensagem).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task MesmoEventoDuasVezes_UmUnicoPar()
    {
        var notificacao = Notificacao();
        var casoDeUso = CasoDeUso();

        await casoDeUso.NotificarAsync(notificacao, TestContext.Current.CancellationToken);
        await casoDeUso.NotificarAsync(notificacao, TestContext.Current.CancellationToken);

        Assert.Equal(2, _notificacoes.Gravadas.Count);
        Assert.Equal(1, _notificacoes.ChamadasAdicionar);
        Assert.Equal(2, _logger.Registros.Count(r => r.Nivel == Microsoft.Extensions.Logging.LogLevel.Information));
        Assert.Equal(2, _metricas.Medicoes.Count(m => m.Nome == "bank.notificacoes.enviadas"));
    }

    [Fact]
    public async Task Notificar_LogsSemNomesNemTexto()
    {
        await CasoDeUso().NotificarAsync(Notificacao(), TestContext.Current.CancellationToken);

        Assert.All(_logger.Registros, r =>
        {
            var texto = r.Mensagem + " " + string.Join(' ', r.Propriedades.Values);
            Assert.DoesNotContain("João Silva", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("Maria Souza", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("Você", texto, StringComparison.Ordinal);
        });
    }
}

/// <summary>Notificações gravadas em memória, com a mesma resposta de "já existem" do repositório real.</summary>
internal sealed class NotificacoesAuditoriaEmMemoria : INotificacoesAuditoria
{
    public List<NotificacaoAuditoria> Gravadas { get; } = [];

    public int ChamadasAdicionar { get; private set; }

    public Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken cancellationToken) =>
        Task.FromResult(Gravadas.Exists(n => n.EventoId == eventoId));

    public Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken cancellationToken)
    {
        ChamadasAdicionar++;
        Gravadas.AddRange(notificacoes);
        return Task.CompletedTask;
    }
}
