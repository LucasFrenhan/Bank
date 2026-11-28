using Bank.SharedKernel;

namespace Bank.Domain.Cartoes;

/// <summary>Identificador de <see cref="LancamentoFatura"/>.</summary>
public readonly record struct LancamentoFaturaId(Guid Valor) : IIdentificador<LancamentoFaturaId>
{
    public static LancamentoFaturaId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static LancamentoFaturaId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
