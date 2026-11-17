using Bank.Domain.Comum;

namespace Bank.Domain.Carteiras;

/// <summary>
/// Carteira digital do usuário (1:1). Saldo nunca negativo, com escala 2, em BRL
/// (RN-04, FR-014 a FR-016). Movimentações chegam com a spec de transferências (issue #8).
/// </summary>
public sealed class Carteira
{
    private Carteira()
    {
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public decimal Saldo { get; private set; }

    public string Moeda { get; private set; } = Dinheiro.MoedaPadrao;

    public DateTimeOffset AtualizadoEm { get; private set; }

    internal static Carteira Criar(Guid usuarioId, decimal saldoInicial, DateTimeOffset agora)
    {
        if (saldoInicial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(saldoInicial), saldoInicial, "O saldo inicial não pode ser negativo.");
        }

        return new Carteira
        {
            Id = Guid.CreateVersion7(agora),
            UsuarioId = usuarioId,
            Saldo = Dinheiro.NormalizarEscala(saldoInicial),
            Moeda = Dinheiro.MoedaPadrao,
            AtualizadoEm = agora,
        };
    }
}
