namespace Bank.Domain.Notificacoes;

/// <summary>
/// Canal de uma notificação (specs/011-notificacoes-auditoria, FR-003, Clarification Q2). Enquanto
/// não há envio real, só <see cref="Simulado"/>; canais reais (e-mail, push) entram como valores novos.
/// </summary>
public enum CanalNotificacao
{
    /// <summary>Nada é enviado: o registro persistido é o "envio". Gravado como <c>simulado</c>.</summary>
    Simulado = 1,
}
