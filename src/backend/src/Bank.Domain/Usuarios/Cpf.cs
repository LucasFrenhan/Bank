using Bank.SharedKernel;

namespace Bank.Domain.Usuarios;

/// <summary>
/// CPF válido (RN-03, FR-004): 11 dígitos, dígitos verificadores pelo módulo 11 e não
/// composto por um único dígito repetido. Aceita a máscara <c>000.000.000-00</c>.
/// </summary>
public sealed record Cpf
{
    public const int Tamanho = 11;

    private Cpf(string valor)
    {
        Valor = valor;
    }

    /// <summary>Somente os 11 dígitos, preservando zeros à esquerda.</summary>
    public string Valor { get; }

    public static Resultado<Cpf> Criar(string? valor)
    {
        var normalizado = Normalizar(valor);

        return EhValido(normalizado)
            ? Resultado.Ok(new Cpf(normalizado))
            : Resultado.Falha<Cpf>(Erro.Validacao(Documento.Campo, "CPF inválido."));
    }

    private static string Normalizar(string? valor) =>
        (valor ?? string.Empty).Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

    private static bool EhValido(string cpf)
    {
        if (cpf.Length != Tamanho || !cpf.All(char.IsAsciiDigit) || cpf.All(c => c == cpf[0]))
        {
            return false;
        }

        return cpf[9] == CalcularDigito(cpf.AsSpan(0, 9)) && cpf[10] == CalcularDigito(cpf.AsSpan(0, 10));
    }

    /// <summary>Pesos decrescentes até 2 (10..2 para o 1º DV; 11..2 para o 2º).</summary>
    private static char CalcularDigito(ReadOnlySpan<char> digitos)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Length; i++)
        {
            soma += (digitos[i] - '0') * (digitos.Length + 1 - i);
        }

        var resto = soma % 11;
        return (char)('0' + (resto < 2 ? 0 : 11 - resto));
    }

    public override string ToString() => Valor;
}
