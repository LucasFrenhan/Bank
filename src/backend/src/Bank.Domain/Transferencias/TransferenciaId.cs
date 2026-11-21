using Bank.Domain.Comum;

namespace Bank.Domain.Transferencias;

/// <summary>Identificador de uma <see cref="Transferencia"/>.</summary>
public readonly record struct TransferenciaId(Guid Valor) : IIdentificador<TransferenciaId>
{
    public static TransferenciaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static TransferenciaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
