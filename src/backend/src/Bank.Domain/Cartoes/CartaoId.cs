using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>Identificador de <see cref="Cartao"/>.</summary>
public readonly record struct CartaoId(Guid Valor) : IIdentificador<CartaoId>
{
    public static CartaoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static CartaoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
