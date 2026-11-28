namespace Bank.SharedKernel;

/// <summary>Regras de valores monetários (constituição, Princípio IV).</summary>
public static class Dinheiro
{
    /// <summary>Moeda única das carteiras (ISO 4217).</summary>
    public const string MoedaPadrao = "BRL";

    /// <summary>Arredonda para 2 casas e fixa a escala em 2 (<c>10m</c> → <c>10.00m</c>).</summary>
    public static decimal NormalizarEscala(decimal valor) => decimal.Round(valor, 2, MidpointRounding.ToEven) + 0.00m;

    /// <summary>Indica se o valor tem no máximo 2 casas decimais.</summary>
    public static bool TemAteDuasCasas(decimal valor) => decimal.Round(valor, 2) == valor;
}
