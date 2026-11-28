using Bank.SharedKernel;

namespace Bank.Domain.Cartoes;

/// <summary>
/// Número de cartão com 16 dígitos válido pelo Luhn. Nunca é gravado nem escrito em log: o
/// <see cref="ToString"/> sai mascarado (specs/014-cartao-credito-cobrancas, FR-008; research R2).
/// </summary>
public sealed class NumeroCartao
{
    public const string Campo = "numeroCartao";
    public const int Tamanho = 16;

    private NumeroCartao(string valor)
    {
        Valor = valor;
    }

    /// <summary>Os 16 dígitos. Use só para exibir ao titular ou comparar.</summary>
    public string Valor { get; }

    public string UltimosDigitos => Valor[^4..];

    public static Resultado<NumeroCartao> Criar(string? numero)
    {
        var digitos = (numero ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

        return digitos.Length == Tamanho && digitos.All(char.IsAsciiDigit) && DigitoLuhn(digitos[..^1]) == digitos[^1]
            ? Resultado.Ok(new NumeroCartao(digitos))
            : Resultado.Falha<NumeroCartao>(Erro.Validacao(Campo, "Informe um número de cartão válido, com 16 dígitos."));
    }

    /// <summary>Monta o número a partir dos 15 primeiros dígitos, com o dígito de Luhn.</summary>
    public static NumeroCartao DePrefixo(string quinzeDigitos)
    {
        ArgumentNullException.ThrowIfNull(quinzeDigitos);
        if (quinzeDigitos.Length != Tamanho - 1 || !quinzeDigitos.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("São necessários exatamente 15 dígitos.", nameof(quinzeDigitos));
        }

        return new NumeroCartao(quinzeDigitos + DigitoLuhn(quinzeDigitos));
    }

    public override string ToString() => $"**** **** **** {UltimosDigitos}";

    private static char DigitoLuhn(string digitos)
    {
        var soma = 0;
        var dobrar = true;
        for (var i = digitos.Length - 1; i >= 0; i--)
        {
            var valor = digitos[i] - '0';
            if (dobrar)
            {
                valor *= 2;
                if (valor > 9)
                {
                    valor -= 9;
                }
            }

            soma += valor;
            dobrar = !dobrar;
        }

        return (char)('0' + ((10 - (soma % 10)) % 10));
    }
}
