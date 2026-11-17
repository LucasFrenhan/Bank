namespace Bank.Domain.Permissoes;

/// <summary>Operações sujeitas a permissão por perfil de conta (RN-06).</summary>
public enum Permissao
{
    RealizarTransferencia = 1,
    ReceberTransferencia = 2,
    PagarCobrancaCartao = 3,
    GerarCobrancaCartao = 4,
}
