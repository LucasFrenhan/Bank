using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Notificacoes;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Notificacoes;

/// <summary>
/// Notifica pagador e recebedor de uma transferência concluída (specs/010-mensageria-rabbitmq,
/// FR-016, research R8; specs/011-notificacoes-auditoria, FR-001–FR-005, research R3, R6).
/// Grava o comprovante de envio (pagador) e o aviso de crédito (recebedor) em
/// <see cref="INotificacoesAuditoria"/>, na transação aberta pelo consumidor, e registra um log
/// estruturado e a métrica por destinatário, sem nome, descrição nem texto (FR-023 da 010). Não há
/// envio real: o canal é <see cref="CanalNotificacao.Simulado"/>. O envio real (e-mail, push) entra aqui.
/// </summary>
public sealed partial class NotificarTransferenciaConcluida(
    MetricasNotificacoes metricas,
    INotificacoesAuditoria notificacoes,
    TimeProvider relogio,
    ILogger<NotificarTransferenciaConcluida> logger) : INotificadorTransferencia
{
    public const string PapelPagador = "pagador";
    public const string PapelRecebedor = "recebedor";

    public async Task NotificarAsync(NotificacaoTransferencia notificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacao);

        // 2ª camada de idempotência (research R3): a 1ª é eventos_processados, no consumidor.
        if (await notificacoes.ExistemParaEventoAsync(notificacao.EventoId, cancellationToken))
        {
            LogJaRegistradas(logger, notificacao.EventoId.Valor);
            return;
        }

        var agora = relogio.GetUtcNow();
        var comprovante = NotificacaoAuditoria.ComprovanteEnvio(
            notificacao.EventoId, notificacao.PagadorUsuarioId, notificacao.Valor, notificacao.RecebedorNome, agora);
        var aviso = NotificacaoAuditoria.AvisoCredito(
            notificacao.EventoId, notificacao.RecebedorUsuarioId, notificacao.Valor, notificacao.PagadorNome, agora);

        await notificacoes.AdicionarAsync([comprovante, aviso], cancellationToken);

        // Depois da gravação; só o commit do consumidor falta (research R6).
        LogNotificacao(logger, PapelPagador, notificacao.PagadorUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.TransferenciaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelPagador);

        LogNotificacao(logger, PapelRecebedor, notificacao.RecebedorUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.TransferenciaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelRecebedor);
    }

    [LoggerMessage(EventId = 5100, EventName = "NotificacaoTransferencia", Level = LogLevel.Information,
        Message = "Notificação de transferência para o {Papel} {UsuarioId}: evento {EventoId}, transferência {TransferenciaId}, valor {Valor}")]
    private static partial void LogNotificacao(ILogger logger, string papel, Guid usuarioId, Guid eventoId, Guid transferenciaId, decimal valor);

    [LoggerMessage(EventId = 5101, EventName = "NotificacoesJaRegistradas", Level = LogLevel.Debug,
        Message = "Notificações do evento {EventoId} já registradas; nada a gravar")]
    private static partial void LogJaRegistradas(ILogger logger, Guid eventoId);
}
