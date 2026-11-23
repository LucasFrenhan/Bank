namespace Bank.Domain.Notificacoes;

/// <summary>
/// Tipo de uma notificação de transferência (specs/011-notificacoes-auditoria, data-model,
/// Clarification Q1). Gravado como <c>comprovante_envio</c> / <c>aviso_credito</c>.
/// </summary>
public enum TipoNotificacao
{
    /// <summary>Comprovante de envio, destinado ao pagador.</summary>
    ComprovanteEnvio = 1,

    /// <summary>Aviso de crédito, destinado ao recebedor.</summary>
    AvisoCredito = 2,
}
