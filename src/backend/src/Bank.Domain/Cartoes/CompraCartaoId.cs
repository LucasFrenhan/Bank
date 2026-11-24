using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>Identificador de <see cref="CompraCartao"/>.</summary>
public readonly record struct CompraCartaoId(Guid Valor) : IIdentificador<CompraCartaoId>
{
    public static CompraCartaoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static CompraCartaoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
