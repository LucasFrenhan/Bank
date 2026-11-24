using Bank.Domain.Comum;

namespace Bank.Domain.Recebiveis;

/// <summary>Identificador de <see cref="Recebivel"/>.</summary>
public readonly record struct RecebivelId(Guid Valor) : IIdentificador<RecebivelId>
{
    public static RecebivelId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static RecebivelId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
