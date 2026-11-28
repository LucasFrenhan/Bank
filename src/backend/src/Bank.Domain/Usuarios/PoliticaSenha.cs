using Bank.SharedKernel;

namespace Bank.Domain.Usuarios;

/// <summary>
/// Política de senha (FR-009): 8–128 caracteres, com ao menos uma letra maiúscula, uma
/// minúscula, um dígito e um caractere especial. Cada regra violada gera um erro próprio.
/// </summary>
public static class PoliticaSenha
{
    public const string Campo = "senha";
    public const int TamanhoMinimo = 8;
    public const int TamanhoMaximo = 128;

    public static IReadOnlyList<Erro> Validar(string? senha)
    {
        if (string.IsNullOrEmpty(senha))
        {
            return [Erro.Validacao(Campo, "A senha é obrigatória.")];
        }

        var erros = new List<Erro>();

        if (senha.Length is < TamanhoMinimo or > TamanhoMaximo)
        {
            erros.Add(Erro.Validacao(Campo, $"A senha deve ter entre {TamanhoMinimo} e {TamanhoMaximo} caracteres."));
        }

        if (!senha.Any(char.IsUpper))
        {
            erros.Add(Erro.Validacao(Campo, "A senha deve conter ao menos uma letra maiúscula."));
        }

        if (!senha.Any(char.IsLower))
        {
            erros.Add(Erro.Validacao(Campo, "A senha deve conter ao menos uma letra minúscula."));
        }

        if (!senha.Any(char.IsDigit))
        {
            erros.Add(Erro.Validacao(Campo, "A senha deve conter ao menos um dígito."));
        }

        if (senha.All(char.IsLetterOrDigit))
        {
            erros.Add(Erro.Validacao(Campo, "A senha deve conter ao menos um caractere especial."));
        }

        return erros;
    }
}
