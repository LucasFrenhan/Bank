using Bank.Domain.Comum;

namespace Bank.Domain.Transferencias;

public enum TipoLancamento
{
    Abertura = 1,
    Transferencia = 2,
}

public enum DirecaoLancamento
{
    Entrada = 1,
    Saida = 2,
}

/// <summary>
/// Entrada imutável do razão de uma carteira (FR-008, FR-015): o saldo é a soma das entradas
/// menos as saídas. Abertura (saldo de demonstração) ou um dos lados de uma transferência.
/// </summary>
public sealed class Lancamento
{
    public const string DescricaoAbertura = "Saldo inicial de demonstração";

    private Lancamento()
    {
    }

    public Guid Id { get; private set; }

    public Guid CarteiraId { get; private set; }

    public TipoLancamento Tipo { get; private set; }

    public DirecaoLancamento Direcao { get; private set; }

    public decimal Valor { get; private set; }

    public Guid? TransferenciaId { get; private set; }

    public string? Descricao { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>Entrada do saldo de demonstração atribuído no cadastro (spec 003, RN-05).</summary>
    public static Lancamento Abertura(Guid carteiraId, decimal valor, DateTimeOffset agora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        return new Lancamento
        {
            Id = Guid.CreateVersion7(agora),
            CarteiraId = carteiraId,
            Tipo = TipoLancamento.Abertura,
            Direcao = DirecaoLancamento.Entrada,
            Valor = Dinheiro.NormalizarEscala(valor),
            Descricao = DescricaoAbertura,
            CriadoEm = agora,
        };
    }

    internal static Lancamento DeTransferencia(
        Guid carteiraId,
        DirecaoLancamento direcao,
        decimal valor,
        Guid transferenciaId,
        string? descricao,
        DateTimeOffset agora) => new()
        {
            Id = Guid.CreateVersion7(agora),
            CarteiraId = carteiraId,
            Tipo = TipoLancamento.Transferencia,
            Direcao = direcao,
            Valor = valor,
            TransferenciaId = transferenciaId,
            Descricao = descricao,
            CriadoEm = agora,
        };
}
