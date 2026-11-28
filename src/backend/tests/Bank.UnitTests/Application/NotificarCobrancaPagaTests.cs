using Bank.Application.Abstracoes;
using Bank.Application.Notificacoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Cobrancas;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.UnitTests.Application;

/// <summary>Notificações de cobrança paga (specs/014-cartao-credito-cobrancas, US7; FR-035).</summary>
public sealed class NotificarCobrancaPagaTests : IDisposable
{
    private readonly MetricasCapturadasBase _metricas = new(f => new MetricasNotificacoes(f));
    private readonly LoggerCapturado<NotificarCobrancaPaga> _logger = new();
    private readonly NotificacoesAuditoriaEmMemoria _notificacoes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _metricas.Dispose();

    private NotificarCobrancaPaga CasoDeUso() =>
        new((MetricasNotificacoes)_metricas.Instancia, _notificacoes, new RelogioFixo(CartoesDeTeste.Agora), _logger);

    private static NotificacaoCobranca Notificacao(string? lojista = "Cafeteria Ltda") => new(
        EventoId.De(Guid.CreateVersion7()), CobrancaId.De(Guid.CreateVersion7()), UsuarioId.De(Guid.CreateVersion7()), UsuarioId.De(Guid.CreateVersion7()), 1299.90m, lojista);

    [Theory]
    [InlineData("Cafeteria Ltda", "Você pagou R$ 1.299,90 para Cafeteria Ltda no cartão de crédito.")]
    [InlineData(null, "Você pagou R$ 1.299,90 no cartão de crédito.")]
    [InlineData("  ", "Você pagou R$ 1.299,90 no cartão de crédito.")]
    public async Task Grava_ComprovanteEAviso(string? lojista, string texto)
    {
        var notificacao = Notificacao(lojista);

        await CasoDeUso().NotificarAsync(notificacao, Ct);

        var pagador = Assert.Single(_notificacoes.Gravadas, n => n.Tipo == TipoNotificacao.ComprovanteCobranca);
        Assert.Equal((notificacao.PagadorUsuarioId, texto, notificacao.EventoId, CanalNotificacao.Simulado), (pagador.UsuarioDestinoId, pagador.Mensagem, pagador.EventoId, pagador.Canal));
        var aviso = Assert.Single(_notificacoes.Gravadas, n => n.Tipo == TipoNotificacao.CobrancaPaga);
        Assert.Equal((notificacao.LojistaUsuarioId, "A sua cobrança de R$ 1.299,90 foi paga."), (aviso.UsuarioDestinoId, aviso.Mensagem));
        Assert.Equal(["lojista", "pagador"], _metricas.Medicoes.Select(m => m.Tags["papel"]).Order(StringComparer.Ordinal));
        Assert.Equal(2, _logger.Registros.Count);
        Assert.All(_logger.Registros, r => Assert.DoesNotContain("Cafeteria", r.Mensagem, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EventoRepetido_NadaNovo()
    {
        var notificacao = Notificacao();
        await CasoDeUso().NotificarAsync(notificacao, Ct);

        await CasoDeUso().NotificarAsync(notificacao, Ct);

        Assert.Equal((2, 1), (_notificacoes.Gravadas.Count, _notificacoes.ChamadasAdicionar));
    }
}
