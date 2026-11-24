using Bank.Domain.Comum;

namespace Bank.Domain.Cobrancas;

/// <summary>Identificador de <see cref="Cobranca"/>.</summary>
public readonly record struct CobrancaId(Guid Valor) : IIdentificador<CobrancaId>
{
    public static CobrancaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static CobrancaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
