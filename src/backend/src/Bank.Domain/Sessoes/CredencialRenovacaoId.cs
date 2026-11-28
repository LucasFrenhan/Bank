using Bank.SharedKernel;

namespace Bank.Domain.Sessoes;

/// <summary>Identificador de uma <see cref="Sessoes.CredencialRenovacao"/> (specs/016-sessao-refresh-token).</summary>
public readonly record struct CredencialRenovacaoId(Guid Valor) : IIdentificador<CredencialRenovacaoId>
{
    public static CredencialRenovacaoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static CredencialRenovacaoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
