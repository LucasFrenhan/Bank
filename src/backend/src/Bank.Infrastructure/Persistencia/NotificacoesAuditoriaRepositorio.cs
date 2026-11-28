using Bank.Application.Abstracoes;
using Bank.Domain.Notificacoes;
using Bank.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Notificações de auditoria (specs/011-notificacoes-auditoria, research R2). Grava no
/// <see cref="BankDbContext"/> do escopo, participando da transação já aberta pelo consumidor;
/// não abre transação própria.
/// </summary>
internal sealed class NotificacoesAuditoriaRepositorio(BankDbContext contexto) : INotificacoesAuditoria
{
    public Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken cancellationToken) =>
        contexto.NotificacoesAuditoria.AnyAsync(n => n.EventoId == eventoId, cancellationToken);

    public async Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacoes);

        contexto.NotificacoesAuditoria.AddRange(notificacoes);
        await contexto.SaveChangesAsync(cancellationToken);
    }
}
