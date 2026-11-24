using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>Identificador de <see cref="AutorizacaoCartao"/>.</summary>
public readonly record struct AutorizacaoCartaoId(Guid Valor) : IIdentificador<AutorizacaoCartaoId>
{
    public static AutorizacaoCartaoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static AutorizacaoCartaoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
