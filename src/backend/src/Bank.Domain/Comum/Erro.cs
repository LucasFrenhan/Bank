namespace Bank.Domain.Comum;

/// <summary>Natureza de uma falha esperada; define o status HTTP na camada de transporte.</summary>
public enum TipoErro
{
    Validacao,
    Conflito,
    NaoAutorizado,
    NaoEncontrado,

    /// <summary>Regra de negócio violada com dados válidos (422).</summary>
    RegraDeNegocio,
}

/// <summary>
/// Falha esperada de uma regra de negócio. <see cref="Codigo"/> é estável e documentado no
/// contrato (constituição, Princípio III); <see cref="Campo"/> identifica o campo inválido em
/// erros de validação.
/// </summary>
public sealed record Erro(string Codigo, string Mensagem, TipoErro Tipo, string? Campo = null)
{
    public const string CodigoValidacao = "validacao";

    public static Erro Validacao(string campo, string mensagem) =>
        new(CodigoValidacao, mensagem, TipoErro.Validacao, campo);
}
