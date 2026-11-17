using Bank.Domain.Comum;

namespace Bank.Domain.Usuarios;

/// <summary>
/// Documento do titular conforme o tipo de conta (RN-01, RN-03): CPF para
/// <see cref="TipoConta.PessoaFisica"/> e CNPJ para <see cref="TipoConta.PessoaJuridica"/>.
/// </summary>
public sealed record Documento
{
    public const string Campo = "documento";
    public const int TamanhoMaximo = Cnpj.Tamanho;

    private Documento(string valor, TipoConta tipoConta)
    {
        Valor = valor;
        TipoConta = tipoConta;
    }

    /// <summary>Valor normalizado: sem máscara, letras em maiúsculas.</summary>
    public string Valor { get; }

    public TipoConta TipoConta { get; }

    public static Resultado<Documento> Criar(string? valor, TipoConta tipoConta)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Resultado.Falha<Documento>(Erro.Validacao(Campo, "O documento é obrigatório."));
        }

        return tipoConta switch
        {
            TipoConta.PessoaFisica => CriarCpf(valor),
            TipoConta.PessoaJuridica => CriarCnpj(valor),
            _ => throw new ArgumentOutOfRangeException(nameof(tipoConta), tipoConta, "Tipo de conta desconhecido."),
        };
    }

    /// <summary>
    /// Valida o documento sem conhecer o tipo de conta (usado quando o tipo informado é
    /// inválido, para ainda assim reportar todos os campos inválidos de uma vez).
    /// </summary>
    public static Resultado ValidarSemTipo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Resultado.Falha(Erro.Validacao(Campo, "O documento é obrigatório."));
        }

        return Cpf.Criar(valor).Sucesso || Cnpj.Criar(valor).Sucesso
            ? Resultado.Ok()
            : Resultado.Falha(Erro.Validacao(Campo, "Documento inválido: informe um CPF ou CNPJ válido."));
    }

    private static Resultado<Documento> CriarCpf(string valor)
    {
        var cpf = Cpf.Criar(valor);
        if (cpf.Sucesso)
        {
            return Resultado.Ok(new Documento(cpf.Valor.Valor, TipoConta.PessoaFisica));
        }

        return Cnpj.Criar(valor).Sucesso
            ? Resultado.Falha<Documento>(Erro.Validacao(Campo, "Contas PessoaFisica exigem CPF; foi informado um CNPJ."))
            : Resultado.Falha<Documento>(cpf.Erros);
    }

    private static Resultado<Documento> CriarCnpj(string valor)
    {
        var cnpj = Cnpj.Criar(valor);
        if (cnpj.Sucesso)
        {
            return Resultado.Ok(new Documento(cnpj.Valor.Valor, TipoConta.PessoaJuridica));
        }

        return Cpf.Criar(valor).Sucesso
            ? Resultado.Falha<Documento>(Erro.Validacao(Campo, "Contas PessoaJuridica exigem CNPJ; foi informado um CPF."))
            : Resultado.Falha<Documento>(cnpj.Erros);
    }

    public override string ToString() => Valor;
}
