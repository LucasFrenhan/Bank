using Bank.SharedKernel;

namespace Bank.Domain.Cartoes;

/// <summary>Erros de fatura (specs/014-cartao-credito-cobrancas, FR-036).</summary>
public static class ErrosFatura
{
    public static readonly Erro NaoEncontrada =
        new("fatura-nao-encontrada", "Fatura não encontrada.", TipoErro.NaoEncontrado);

    public static readonly Erro NaoDisponivel =
        new("fatura-nao-disponivel", "Esta fatura não está disponível para pagamento.", TipoErro.Conflito);

    /// <summary>Mesmo código das transferências, com texto próprio.</summary>
    public static readonly Erro SaldoInsuficiente =
        new(Transferencias.ErrosTransferencia.SaldoInsuficiente.Codigo, "O saldo disponível não cobre o valor da fatura.", TipoErro.RegraDeNegocio);
}
