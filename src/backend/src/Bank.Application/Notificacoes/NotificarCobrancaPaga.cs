using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Notificacoes;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Notificacoes;

/// <summary>
/// Notifica pagador e lojista de uma cobrança paga (specs/014-cartao-credito-cobrancas, US7; FR-035), com as
/// mesmas garantias de <see cref="NotificarTransferenciaConcluida"/>: grava na transação do consumidor, e log e
/// métrica saem sem nome nem dados de cartão.
/// </summary>
public sealed partial class NotificarCobrancaPaga(
    MetricasNotificacoes metricas,
    INotificacoesAuditoria notificacoes,
    TimeProvider relogio,
    ILogger<NotificarCobrancaPaga> logger) : INotificadorCobranca
{
    public const string PapelPagador = NotificarTransferenciaConcluida.PapelPagador;
    public const string PapelLojista = "lojista";

    public async Task NotificarAsync(NotificacaoCobranca notificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacao);

        // 2ª camada de idempotência: a 1ª é eventos_processados, no consumidor (spec 011, R3).
        if (await notificacoes.ExistemParaEventoAsync(notificacao.EventoId, cancellationToken))
        {
            LogJaRegistradas(logger, notificacao.EventoId.Valor);
            return;
        }

        var agora = relogio.GetUtcNow();
        var comprovante = NotificacaoAuditoria.ComprovanteCobranca(
            notificacao.EventoId, notificacao.PagadorUsuarioId, notificacao.Valor, notificacao.LojistaNome, agora);
        var aviso = NotificacaoAuditoria.CobrancaPaga(notificacao.EventoId, notificacao.LojistaUsuarioId, notificacao.Valor, agora);

        await notificacoes.AdicionarAsync([comprovante, aviso], cancellationToken);

        LogNotificacao(logger, PapelPagador, notificacao.PagadorUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.CobrancaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelPagador);

        LogNotificacao(logger, PapelLojista, notificacao.LojistaUsuarioId.Valor, notificacao.EventoId.Valor, notificacao.CobrancaId.Valor, notificacao.Valor);
        metricas.RegistrarEnviada(PapelLojista);
    }

    [LoggerMessage(EventId = 5110, EventName = "NotificacaoCobranca", Level = LogLevel.Information,
        Message = "Notificação de cobrança paga para o {Papel} {UsuarioId}: evento {EventoId}, cobrança {CobrancaId}, valor {Valor}")]
    private static partial void LogNotificacao(ILogger logger, string papel, Guid usuarioId, Guid eventoId, Guid cobrancaId, decimal valor);

    [LoggerMessage(EventId = 5111, EventName = "NotificacoesDeCobrancaJaRegistradas", Level = LogLevel.Debug,
        Message = "Notificações do evento {EventoId} já registradas; nada a gravar")]
    private static partial void LogJaRegistradas(ILogger logger, Guid eventoId);
}
