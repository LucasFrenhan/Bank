using Bank.Domain.Comum;

namespace Bank.Domain.Cobrancas;

/// <summary>Erros de cobrança (specs/014-cartao-credito-cobrancas, FR-036).</summary>
public static class ErrosCobranca
{
    /// <summary>Inexistente ou, no cancelamento, de outra PJ: mesma resposta.</summary>
    public static readonly Erro NaoEncontrada =
        new("cobranca-nao-encontrada", "Cobrança não encontrada.", TipoErro.NaoEncontrado);

    public static readonly Erro NaoPendente =
        new("cobranca-nao-pendente", "Esta cobrança já foi paga ou cancelada.", TipoErro.Conflito);
}
