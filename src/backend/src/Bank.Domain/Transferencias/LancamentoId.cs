using Bank.SharedKernel;

namespace Bank.Domain.Transferencias;

/// <summary>Identificador de um <see cref="Lancamento"/>.</summary>
public readonly record struct LancamentoId(Guid Valor) : IIdentificador<LancamentoId>
{
    public static LancamentoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static LancamentoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
