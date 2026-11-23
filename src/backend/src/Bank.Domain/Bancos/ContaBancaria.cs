using System.Globalization;

namespace Bank.Domain.Bancos;

/// <summary>
/// Dados bancários de uma conta: banco, agência, número com dígito verificador e tipo
/// (specs/012-dados-bancarios-carteira, FR-002, FR-004; research R1, R3). Value object, sem
/// identidade própria. Todos os campos são texto para preservar zeros à esquerda.
/// </summary>
public sealed record ContaBancaria
{
    public const int TamanhoAgencia = 4;
    public const int TamanhoNumero = 8;
    public const int TamanhoDigito = 1;
    public const long NumeroMaximo = 99_999_999;
    public const string AgenciaPadrao = "0001";

    private ContaBancaria(string codigoBanco, string agencia, string numero, string digito, TipoContaBancaria tipo)
    {
        CodigoBanco = codigoBanco;
        Agencia = agencia;
        Numero = numero;
        Digito = digito;
        Tipo = tipo;
    }

    public string CodigoBanco { get; }

    public string Agencia { get; }

    public string Numero { get; }

    public string Digito { get; }

    public TipoContaBancaria Tipo { get; }

    /// <summary>Cria a conta: formata o número com 8 dígitos, valida e calcula o dígito.</summary>
    public static ContaBancaria Nova(Banco banco, string agencia, long numero, TipoContaBancaria tipo)
    {
        ArgumentNullException.ThrowIfNull(banco);
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numero, NumeroMaximo);
        ValidarAgencia(agencia);
        ValidarTipo(tipo);

        var numeroFormatado = numero.ToString("D8", CultureInfo.InvariantCulture);
        return new ContaBancaria(banco.Codigo, agencia, numeroFormatado, CalcularDigito(agencia, numeroFormatado), tipo);
    }

    /// <summary>Monta a conta a partir de dados já gravados (sem recalcular nem validar).</summary>
    public static ContaBancaria Reconstituir(string codigoBanco, string agencia, string numero, string digito, TipoContaBancaria tipo) =>
        new(codigoBanco, agencia, numero, digito, tipo);

    /// <summary>
    /// Luhn (módulo 10) sobre os dígitos de <paramref name="agencia"/> + <paramref name="numero"/>:
    /// da direita para a esquerda, dobra um dígito sim, outro não (começando pelo mais à direita),
    /// subtrai 9 dos resultados acima de 9 e devolve <c>(10 - soma % 10) % 10</c>. A function SQL
    /// <c>calcular_digito_conta</c> implementa a mesma regra.
    /// </summary>
    public static string CalcularDigito(string agencia, string numero)
    {
        ArgumentNullException.ThrowIfNull(agencia);
        ArgumentNullException.ThrowIfNull(numero);
        var digitos = agencia + numero;

        var soma = 0;
        var dobrar = true;
        for (var i = digitos.Length - 1; i >= 0; i--)
        {
            if (!char.IsAsciiDigit(digitos[i]))
            {
                throw new ArgumentException("A agência e o número devem conter apenas dígitos.", nameof(numero));
            }

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

        return ((10 - (soma % 10)) % 10).ToString(CultureInfo.InvariantCulture);
    }

    private static void ValidarAgencia(string agencia)
    {
        if (agencia is null || agencia.Length != TamanhoAgencia || !agencia.All(char.IsAsciiDigit))
        {
            throw new ArgumentException($"A agência deve ter exatamente {TamanhoAgencia} dígitos.", nameof(agencia));
        }
    }

    private static void ValidarTipo(TipoContaBancaria tipo)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de conta bancária desconhecido.");
        }
    }
}
