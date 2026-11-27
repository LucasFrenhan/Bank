namespace Bank.Domain.Sessoes;

/// <summary>Por que uma sessão foi revogada (specs/016-sessao-refresh-token, data-model).</summary>
public enum MotivoRevogacao
{
    Logout,
    Reuso,
    NovoLogin,
}

/// <summary>
/// Prazos da sessão (specs/016-sessao-refresh-token, FR-002, FR-008): inatividade, limite absoluto desde o login
/// e tolerância para reapresentar uma credencial recém-trocada.
/// </summary>
public sealed record LimitesSessao
{
    public LimitesSessao(TimeSpan inatividade, TimeSpan absoluto, TimeSpan toleranciaReuso)
    {
        if (inatividade <= TimeSpan.Zero || absoluto <= TimeSpan.Zero || toleranciaReuso <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(inatividade), "Os prazos da sessão devem ser positivos.");
        }

        if (inatividade > absoluto)
        {
            throw new ArgumentOutOfRangeException(nameof(inatividade), "A inatividade não pode passar do limite absoluto.");
        }

        if (toleranciaReuso >= inatividade)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranciaReuso), "A tolerância deve ser menor que a inatividade.");
        }

        Inatividade = inatividade;
        Absoluto = absoluto;
        ToleranciaReuso = toleranciaReuso;
    }

    public TimeSpan Inatividade { get; }

    public TimeSpan Absoluto { get; }

    public TimeSpan ToleranciaReuso { get; }
}
