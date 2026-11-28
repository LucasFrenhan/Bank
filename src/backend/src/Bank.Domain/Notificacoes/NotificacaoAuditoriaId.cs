using Bank.SharedKernel;

namespace Bank.Domain.Notificacoes;

/// <summary>Identificador de uma <see cref="NotificacaoAuditoria"/>.</summary>
public readonly record struct NotificacaoAuditoriaId(Guid Valor) : IIdentificador<NotificacaoAuditoriaId>
{
    public static NotificacaoAuditoriaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static NotificacaoAuditoriaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
