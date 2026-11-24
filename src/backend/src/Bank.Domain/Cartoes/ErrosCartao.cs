using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>Erros de cartão (specs/014-cartao-credito-cobrancas, FR-036; contracts/cartoes-cobrancas-api.md).</summary>
public static class ErrosCartao
{
    public static readonly Erro JaExistente =
        new("cartao-ja-existente", "Você já tem um cartão de crédito. Cada CPF pode ter no máximo um.", TipoErro.Conflito);

    public static readonly Erro NaoEncontrado =
        new("cartao-nao-encontrado", "Você ainda não tem um cartão de crédito.", TipoErro.NaoEncontrado);

    /// <summary>Único para qualquer dado que não corresponda ao cartão do pagador (FR-016).</summary>
    public static readonly Erro NaoAutorizado =
        new("cartao-nao-autorizado", "O cartão não foi autorizado pela bandeira.", TipoErro.RegraDeNegocio);

    public static readonly Erro BloqueadoPorFaturaVencida =
        new("cartao-bloqueado-fatura-vencida", "O cartão está bloqueado por fatura vencida. Pague a fatura para voltar a usá-lo.", TipoErro.RegraDeNegocio);

    public static readonly Erro LimiteInsuficiente =
        new("limite-cartao-insuficiente", "O limite disponível do cartão não cobre o valor.", TipoErro.RegraDeNegocio);
}
