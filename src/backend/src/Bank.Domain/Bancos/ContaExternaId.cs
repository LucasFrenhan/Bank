using Bank.Domain.Comum;

namespace Bank.Domain.Bancos;

/// <summary>Identificador de uma <see cref="ContaExterna"/>.</summary>
public readonly record struct ContaExternaId(Guid Valor) : IIdentificador<ContaExternaId>
{
    public static ContaExternaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static ContaExternaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
