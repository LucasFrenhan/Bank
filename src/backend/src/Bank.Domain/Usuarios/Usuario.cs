using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Bank.Domain.Comum;

namespace Bank.Domain.Usuarios;

/// <summary>
/// Titular de uma conta PF ou PJ (raiz de agregado). Nasce sempre com exatamente uma
/// <see cref="Carteiras.Carteira"/> (RN-04). O tipo de conta é imutável (RN-01).
/// </summary>
public sealed class Usuario
{
    public const string CampoNome = "nomeCompleto";
    public const int TamanhoMaximoNome = 150;

    private Usuario()
    {
    }

    public UsuarioId Id { get; private set; }

    public string NomeCompleto { get; private set; } = string.Empty;

    /// <summary>E-mail normalizado (ver <see cref="Usuarios.Email"/>).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Documento normalizado (ver <see cref="Usuarios.Documento"/>).</summary>
    public string Documento { get; private set; } = string.Empty;

    public TipoConta TipoConta { get; private init; }

    public string SenhaHash { get; private set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; private set; }

    public Carteira Carteira { get; private set; } = null!;

    /// <summary>
    /// Lançamento de abertura do saldo de demonstração, gravado junto com o cadastro
    /// (specs/006-transferencias, FR-016); nulo quando o saldo inicial é zero. Não é persistido
    /// como parte do usuário.
    /// </summary>
    public Transferencias.Lancamento? LancamentoDeAbertura { get; private set; }

    /// <summary>Nome completo ou razão social: 1–150 caracteres após remover espaços nas extremidades.</summary>
    public static Resultado<string> ValidarNome(string? nome)
    {
        var normalizado = (nome ?? string.Empty).Trim();

        if (normalizado.Length == 0)
        {
            return Resultado.Falha<string>(Erro.Validacao(CampoNome, "O nome completo é obrigatório."));
        }

        return normalizado.Length > TamanhoMaximoNome
            ? Resultado.Falha<string>(Erro.Validacao(CampoNome, $"O nome completo deve ter no máximo {TamanhoMaximoNome} caracteres."))
            : Resultado.Ok(normalizado);
    }

    /// <summary>
    /// Cria o usuário e sua carteira em uma única operação (RN-04). O tipo de conta vem do
    /// documento, garantindo CPF para PF e CNPJ para PJ. A carteira recebe os dados bancários
    /// <paramref name="conta"/> (specs/012-dados-bancarios-carteira, FR-005).
    /// </summary>
    public static Usuario Criar(
        string nomeCompleto,
        Email email,
        Documento documento,
        string senhaHash,
        ContaBancaria conta,
        decimal saldoInicial,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentException.ThrowIfNullOrWhiteSpace(senhaHash);

        var nome = ValidarNome(nomeCompleto);
        if (!nome.Sucesso)
        {
            throw new ArgumentException(nome.Erros[0].Mensagem, nameof(nomeCompleto));
        }

        var id = UsuarioId.Novo(agora);
        var carteira = Carteira.Criar(id, conta, saldoInicial, agora);

        return new Usuario
        {
            Id = id,
            NomeCompleto = nome.Valor,
            Email = email.Valor,
            Documento = documento.Valor,
            TipoConta = documento.TipoConta,
            SenhaHash = senhaHash,
            CriadoEm = agora,
            Carteira = carteira,
            LancamentoDeAbertura = carteira.Saldo > 0 ? Transferencias.Lancamento.Abertura(carteira.Id, carteira.Saldo, agora) : null,
        };
    }

    /// <summary>Substitui o hash após rehash (algoritmo/parâmetros atualizados).</summary>
    public void AtualizarSenhaHash(string novoHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(novoHash);
        SenhaHash = novoHash;
    }
}
