using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Application.Abstracoes;

/// <summary>
/// O que uma notificação de transferência precisa (specs/010-mensageria-rabbitmq, data-model;
/// specs/011-notificacoes-auditoria, research R4). Os nomes, opcionais (eventos antigos não os
/// têm), entram só no texto persistido da notificação; continuam fora de logs, spans e métricas
/// (spec 011, FR-010). A descrição da transferência não entra (spec 011, FR-004).
/// </summary>
public sealed record NotificacaoTransferencia(
    EventoId EventoId,
    TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId,
    UsuarioId RecebedorUsuarioId,
    decimal Valor,
    DateTimeOffset OcorridoEm,
    string? PagadorNome = null,
    string? RecebedorNome = null);

/// <summary>
/// Efeito do consumo de <c>transferencia.concluida</c> (research R8). Implementado pelo caso de uso
/// <c>NotificarTransferenciaConcluida</c>; nos testes, por dublês que simulam falhas.
/// </summary>
public interface INotificadorTransferencia
{
    Task NotificarAsync(NotificacaoTransferencia notificacao, CancellationToken cancellationToken);
}
