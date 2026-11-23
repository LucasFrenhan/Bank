namespace Bank.Domain.Bancos;

/// <summary>
/// Conta registrada em um dos bancos 002 a 005 para simular a integração com outros bancos
/// (specs/012-dados-bancarios-carteira, FR-011, FR-014; research R6). É só um registro: sem
/// usuário, senha nem saldo, e sem vínculo com carteiras. Gravada apenas pelo script
/// <c>scripts/sql/dados-bancarios/registrar-contas-outros-bancos.sql</c>, por isso não tem
/// fábrica pública.
/// </summary>
public sealed class ContaExterna
{
    public const int TamanhoMaximoTitularNome = 150;
    public const int TamanhoMaximoTitularDocumento = 14;

    private ContaExterna()
    {
    }

    public ContaExternaId Id { get; private set; }

    public string CodigoBanco { get; private set; } = string.Empty;

    public string Agencia { get; private set; } = string.Empty;

    public string NumeroConta { get; private set; } = string.Empty;

    public string DigitoConta { get; private set; } = string.Empty;

    public TipoContaBancaria TipoContaBancaria { get; private set; }

    public string TitularNome { get; private set; } = string.Empty;

    public string TitularDocumento { get; private set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; private set; }

    public ContaBancaria ContaBancaria =>
        ContaBancaria.Reconstituir(CodigoBanco, Agencia, NumeroConta, DigitoConta, TipoContaBancaria);
}
