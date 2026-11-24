using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>Identificador de <see cref="Fatura"/>.</summary>
public readonly record struct FaturaId(Guid Valor) : IIdentificador<FaturaId>
{
    public static FaturaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static FaturaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
