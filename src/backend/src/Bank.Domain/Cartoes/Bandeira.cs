namespace Bank.Domain.Cartoes;

/// <summary>
/// Bandeira de cartão (specs/014-cartao-credito-cobrancas, FR-001). A chave é o código de 3 dígitos
/// (dado natural, como em <see cref="Bancos.Banco"/>).
/// </summary>
public sealed class Bandeira
{
    public const string CodigoVisa = "001";
    public const string CodigoMastercard = "002";
    public const int TamanhoMaximoNome = 30;

    public static readonly Bandeira Visa = new(CodigoVisa, "Visa");
    public static readonly Bandeira Mastercard = new(CodigoMastercard, "Mastercard");

    public static readonly IReadOnlyList<Bandeira> Todas = [Visa, Mastercard];

    private Bandeira()
    {
    }

    private Bandeira(string codigo, string nome)
    {
        Codigo = codigo;
        Nome = nome;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;

    /// <summary>A bandeira do código, ou nula se não existir.</summary>
    public static Bandeira? DeCodigo(string? codigo) =>
        Todas.FirstOrDefault(b => string.Equals(b.Codigo, codigo, StringComparison.Ordinal));

    /// <summary>Bandeira pelo primeiro dígito do número (4 Visa, 5 Mastercard), ou nula (research R11).</summary>
    public static Bandeira? DoNumero(NumeroCartao numero)
    {
        ArgumentNullException.ThrowIfNull(numero);

        return numero.Valor[0] switch
        {
            '4' => Visa,
            '5' => Mastercard,
            _ => null,
        };
    }
}
