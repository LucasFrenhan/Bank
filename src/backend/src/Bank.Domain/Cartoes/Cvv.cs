using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>
/// Código de segurança de 3 dígitos. Nunca é gravado nem escrito em log
/// (specs/014-cartao-credito-cobrancas, FR-008; research R2).
/// </summary>
public sealed class Cvv
{
    public const string Campo = "cvv";
    public const int Tamanho = 3;

    private Cvv(string valor)
    {
        Valor = valor;
    }

    /// <summary>Os 3 dígitos. Use só para exibir ao titular ou comparar.</summary>
    public string Valor { get; }

    public static Resultado<Cvv> Criar(string? cvv) =>
        cvv is { Length: Tamanho } && cvv.All(char.IsAsciiDigit)
            ? Resultado.Ok(new Cvv(cvv))
            : Resultado.Falha<Cvv>(Erro.Validacao(Campo, "O CVV deve ter exatamente 3 dígitos."));

    public override string ToString() => "***";
}
