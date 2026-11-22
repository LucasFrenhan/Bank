using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>
/// O que uma notificação de transferência precisa (specs/010-mensageria-rabbitmq, data-model).
/// Nomes e descrição chegam no evento, mas não entram aqui: a notificação simulada é um log, e
/// logs não levam nome nem descrição (FR-023).
/// </summary>
public sealed record NotificacaoTransferencia(
    EventoId EventoId,
    TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId,
    UsuarioId RecebedorUsuarioId,
    decimal Valor,
    DateTimeOffset OcorridoEm);

/// <summary>
/// Efeito do consumo de <c>transferencia.concluida</c> (research R8). Implementado pelo caso de uso
/// <c>NotificarTransferenciaConcluida</c>; nos testes, por dublês que simulam falhas.
/// </summary>
public interface INotificadorTransferencia
{
    Task NotificarAsync(NotificacaoTransferencia notificacao, CancellationToken cancellationToken);
}
