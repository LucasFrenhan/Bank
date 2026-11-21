using Bank.Application.Abstracoes;
using Bank.Application.Notificacoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Notificação simulada de transferência (specs/010-mensageria-rabbitmq, FR-016, FR-023; research R8).</summary>
public sealed class NotificarTransferenciaConcluidaTests : IDisposable
{
    private readonly MetricasCapturadasBase _metricas = new(f => new MetricasNotificacoes(f));
    private readonly LoggerCapturado<NotificarTransferenciaConcluida> _logger = new();

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task Notificar_RegistraUmLogEUmaMetricaPorDestinatario()
    {
        var notificacao = new NotificacaoTransferencia(
            EventoId.De(Guid.CreateVersion7()),
            TransferenciaId.De(Guid.CreateVersion7()),
            UsuarioId.De(Guid.CreateVersion7()),
            UsuarioId.De(Guid.CreateVersion7()),
            12.34m,
            DateTimeOffset.UtcNow);
        var casoDeUso = new NotificarTransferenciaConcluida((MetricasNotificacoes)_metricas.Instancia, _logger);

        await casoDeUso.NotificarAsync(notificacao, TestContext.Current.CancellationToken);

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
}
