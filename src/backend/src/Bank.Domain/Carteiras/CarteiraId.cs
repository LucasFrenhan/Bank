using Bank.SharedKernel;

namespace Bank.Domain.Carteiras;

/// <summary>Identificador de uma <see cref="Carteira"/>.</summary>
public readonly record struct CarteiraId(Guid Valor) : IIdentificador<CarteiraId>
{
    public static CarteiraId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static CarteiraId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
