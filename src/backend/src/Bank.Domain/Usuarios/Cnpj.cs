using Bank.Domain.Comum;

namespace Bank.Domain.Usuarios;

/// <summary>
/// CNPJ válido, numérico ou alfanumérico (IN RFB nº 2.229/2024; RN-03, FR-005; research R2):
/// 14 caracteres, as 12 primeiras posições em <c>[0-9A-Z]</c> e os 2 dígitos verificadores em
/// <c>[0-9]</c>. Cada caractere vale seu código ASCII − 48; o DV usa módulo 11 com pesos
/// <c>5,4,3,2,9,8,7,6,5,4,3,2</c> (1º) e <c>6,5,4,3,2,9,8,7,6,5,4,3,2</c> (2º). Para CNPJs só com
/// dígitos o cálculo coincide com o algoritmo numérico tradicional.
/// Aceita a máscara <c>XX.XXX.XXX/XXXX-00</c> e letras minúsculas.
/// </summary>
public sealed record Cnpj
{
    public const int Tamanho = 14;

    private static readonly int[] PesosPrimeiroDigito = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosSegundoDigito = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cnpj(string valor)
    {
        Valor = valor;
    }

    /// <summary>Os 14 caracteres sem máscara, letras em maiúsculas.</summary>
    public string Valor { get; }

    public static Resultado<Cnpj> Criar(string? valor)
    {
        var normalizado = Normalizar(valor);

        return EhValido(normalizado)
            ? Resultado.Ok(new Cnpj(normalizado))
            : Resultado.Falha<Cnpj>(Erro.Validacao(Documento.Campo, "CNPJ inválido."));
    }

    private static string Normalizar(string? valor) =>
        (valor ?? string.Empty).Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

    private static bool EhValido(string cnpj)
    {
        if (cnpj.Length != Tamanho || cnpj.All(c => c == cnpj[0]))
        {
            return false;
        }

        for (var i = 0; i < 12; i++)
        {
            if (!char.IsAsciiDigit(cnpj[i]) && !char.IsAsciiLetterUpper(cnpj[i]))
            {
                return false;
            }
        }

        if (!char.IsAsciiDigit(cnpj[12]) || !char.IsAsciiDigit(cnpj[13]))
        {
            return false;
        }

        return cnpj[12] == CalcularDigito(cnpj.AsSpan(0, 12), PesosPrimeiroDigito)
            && cnpj[13] == CalcularDigito(cnpj.AsSpan(0, 13), PesosSegundoDigito);
    }

    private static char CalcularDigito(ReadOnlySpan<char> caracteres, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < caracteres.Length; i++)
        {
            soma += (caracteres[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return (char)('0' + (resto < 2 ? 0 : 11 - resto));
    }

    public override string ToString() => Valor;
}
