namespace Bank.Domain.Permissoes;

/// <summary>Operações sujeitas a permissão por perfil de conta (RN-06).</summary>
public enum Permissao
{
    RealizarTransferencia = 1,
    ReceberTransferencia = 2,
    PagarCobrancaCartao = 3,
    GerarCobrancaCartao = 4,

    /// <summary>Solicitar e consultar o cartão, consultar e pagar faturas (spec 014, R16).</summary>
    UsarCartaoCredito = 5,

    /// <summary>Consultar e antecipar recebíveis (spec 014, R16).</summary>
    GerenciarRecebiveis = 6,
}
