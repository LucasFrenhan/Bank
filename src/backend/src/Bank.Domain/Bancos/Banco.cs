namespace Bank.Domain.Bancos;

/// <summary>
/// Um dos cinco bancos conhecidos (specs/012-dados-bancarios-carteira, FR-001; research R5).
/// O 001 é o nosso banco ("One Bank"); os demais têm nomes simbólicos e existem só para simular
/// outros bancos. A chave é o código de 3 dígitos (dado natural, não um identificador gerado).
/// </summary>
public sealed class Banco
{
    public const string CodigoOneBank = "001";
    public const int TamanhoCodigo = 3;
    public const int TamanhoMaximoNome = 60;

    public static readonly Banco OneBank = new(CodigoOneBank, "One Bank");
    public static readonly Banco Aurora = new("002", "Banco Aurora");
    public static readonly Banco Horizonte = new("003", "Banco Horizonte");
    public static readonly Banco Atlantico = new("004", "Banco Atlântico");
    public static readonly Banco Cerrado = new("005", "Banco Cerrado");

    /// <summary>Os cinco bancos, em ordem de código.</summary>
    public static readonly IReadOnlyList<Banco> Todos = [OneBank, Aurora, Horizonte, Atlantico, Cerrado];

    private Banco()
    {
    }

    private Banco(string codigo, string nome)
    {
        Codigo = codigo;
        Nome = nome;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nome { get; private set; } = string.Empty;
}
