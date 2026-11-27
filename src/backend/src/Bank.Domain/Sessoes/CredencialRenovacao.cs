namespace Bank.Domain.Sessoes;

/// <summary>
/// Um elo da família de credenciais de uma <see cref="Sessao"/> (specs/016-sessao-refresh-token, data-model). Só a
/// mais recente fica ativa (<see cref="RotacionadaEm"/> nulo); as demais são histórico para detectar reuso.
/// </summary>
public sealed class CredencialRenovacao
{
    private CredencialRenovacao()
    {
    }

    public CredencialRenovacaoId Id { get; private set; }

    public SessaoId SessaoId { get; private set; }

    public HashCredencial Hash { get; private set; } = null!;

    public DateTimeOffset EmitidaEm { get; private set; }

    /// <summary>Vencimento: o menor entre emissão + inatividade e o limite absoluto da sessão.</summary>
    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? RotacionadaEm { get; private set; }

    public CredencialRenovacaoId? SubstitutaId { get; private set; }

    public bool Ativa => RotacionadaEm is null;

    internal static CredencialRenovacao Emitir(SessaoId sessaoId, HashCredencial hash, DateTimeOffset agora, DateTimeOffset expiraEm)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return new CredencialRenovacao
        {
            Id = CredencialRenovacaoId.Novo(agora),
            SessaoId = sessaoId,
            Hash = hash,
            EmitidaEm = agora,
            ExpiraEm = expiraEm,
        };
    }

    internal void Rotacionar(DateTimeOffset agora, CredencialRenovacaoId substituta)
    {
        if (!Ativa)
        {
            throw new InvalidOperationException("A credencial já foi trocada.");
        }

        RotacionadaEm = agora;
        SubstitutaId = substituta;
    }
}
