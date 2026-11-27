using Bank.Domain.Comum;

namespace Bank.Domain.Sessoes;

/// <summary>Identificador de uma <see cref="Sessoes.Sessao"/> (specs/016-sessao-refresh-token).</summary>
public readonly record struct SessaoId(Guid Valor) : IIdentificador<SessaoId>
{
    public static SessaoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static SessaoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
