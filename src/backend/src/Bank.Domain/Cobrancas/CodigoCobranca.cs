using System.Security.Cryptography;

namespace Bank.Domain.Cobrancas;

/// <summary>
/// Código curto da cobrança, fácil de ler e ditar (specs/015-frontend-cartao-credito, FR-009a; research R1): 8
/// caracteres aleatórios, sem os que se confundem (0, 1, I, L, O e U), exibidos como <c>XXXX-XXXX</c>.
/// </summary>
public sealed record CodigoCobranca
{
    public const int Tamanho = 8;
    public const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    private CodigoCobranca(string valor)
    {
        Valor = valor;
    }

    /// <summary>Os 8 caracteres, sem hífen.</summary>
    public string Valor { get; }

    public static CodigoCobranca Novo() => new(RandomNumberGenerator.GetString(Alfabeto, Tamanho));

    /// <summary>Aceita minúsculas, espaços e hífens; nulo se malformado.</summary>
    public static CodigoCobranca? Criar(string? texto)
    {
        var valor = (texto ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return valor.Length == Tamanho && valor.All(c => Alfabeto.Contains(c, StringComparison.Ordinal)) ? new CodigoCobranca(valor) : null;
    }

    public override string ToString() => $"{Valor[..4]}-{Valor[4..]}";
}
