using Bank.Domain.Notificacoes;
using Bank.SharedKernel;

namespace Bank.Application.Abstracoes;

/// <summary>
/// Persistência das notificações de auditoria (specs/011-notificacoes-auditoria, research R2).
/// A implementação grava no <c>DbContext</c> do escopo, dentro da transação aberta pelo consumidor
/// para <c>eventos_processados</c>, sem transação própria: o commit (e o ACK) é do consumidor.
/// </summary>
public interface INotificacoesAuditoria
{
    /// <summary>Se o evento já tem notificações gravadas (idempotência no caso de uso — R3).</summary>
    Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken cancellationToken);

    Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken cancellationToken);
}
