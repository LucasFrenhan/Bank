using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Application.Abstracoes;

/// <summary>
/// O que a notificação de uma cobrança paga precisa (specs/014-cartao-credito-cobrancas, FR-035). Sem dados do
/// cartão; o nome do lojista entra só no texto persistido.
/// </summary>
public sealed record NotificacaoCobranca(
    EventoId EventoId,
    CobrancaId CobrancaId,
    UsuarioId LojistaUsuarioId,
    UsuarioId PagadorUsuarioId,
    decimal Valor,
    string? LojistaNome);

/// <summary>Efeito do consumo de <c>cobranca.paga</c>; nos testes, trocado por dublês que simulam falhas.</summary>
public interface INotificadorCobranca
{
    Task NotificarAsync(NotificacaoCobranca notificacao, CancellationToken cancellationToken);
}
