using System.Text.RegularExpressions;
using Bank.SharedKernel;

namespace Bank.Domain.Usuarios;

/// <summary>E-mail normalizado (sem espaços nas extremidades, minúsculo) — FR-007.</summary>
public sealed partial record Email
{
    public const string Campo = "email";
    public const int TamanhoMaximo = 150;

    private Email(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; }

    public static string Normalizar(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant();

    public static Resultado<Email> Criar(string? valor)
    {
        var normalizado = Normalizar(valor);

        if (normalizado.Length == 0)
        {
            return Resultado.Falha<Email>(Erro.Validacao(Campo, "O e-mail é obrigatório."));
        }

        if (normalizado.Length > TamanhoMaximo)
        {
            return Resultado.Falha<Email>(Erro.Validacao(Campo, $"O e-mail deve ter no máximo {TamanhoMaximo} caracteres."));
        }

        return FormatoEmail().IsMatch(normalizado)
            ? Resultado.Ok(new Email(normalizado))
            : Resultado.Falha<Email>(Erro.Validacao(Campo, "O e-mail não tem um formato válido."));
    }

    public override string ToString() => Valor;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoEmail();
}
