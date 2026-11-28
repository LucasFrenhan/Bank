using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;

namespace Bank.Domain.Transferencias;

/// <summary>Erros de negócio de transferências (códigos do contrato — specs/006-transferencias).</summary>
public static class ErrosTransferencia
{
    public static readonly Erro DestinatarioNaoEncontrado =
        new("destinatario-nao-encontrado", "Nenhuma conta Bank com esse documento.", TipoErro.NaoEncontrado);

    public static readonly Erro Autotransferencia =
        new("autotransferencia", "Não é possível transferir para a própria conta.", TipoErro.RegraDeNegocio);

    public static readonly Erro SaldoInsuficiente =
        new("saldo-insuficiente", "O saldo disponível não cobre o valor da transferência.", TipoErro.RegraDeNegocio);

    public static readonly Erro IdempotenciaDivergente =
        new(ErrosIdempotencia.CodigoDivergente, "Esta chave de idempotência já foi usada com outro destinatário ou valor.", TipoErro.Conflito);

    /// <summary>
    /// Valor acima de um limite de transferência (specs/009-limite-transferencia, FR-011): um único
    /// código, com o tipo, o limite e o valor disponível nos detalhes.
    /// </summary>
    public static Erro LimiteExcedido(TipoLimite tipo, decimal limite, decimal valorDisponivel) =>
        new(
            "limite-excedido",
            tipo == TipoLimite.PorOperacao
                ? "O valor passa do limite por transferência."
                : "O valor passa do limite diário de transferências.",
            TipoErro.RegraDeNegocio,
            Detalhes: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tipoLimite"] = tipo.ToString(),
                ["limite"] = limite,
                ["valorDisponivel"] = valorDisponivel,
            });

    /// <summary>
    /// Transferência inexistente ou de que o usuário não participa — mesma resposta nos dois casos
    /// (specs/008-consulta-transferencia, FR-004).
    /// </summary>
    public static readonly Erro TransferenciaNaoEncontrada =
        new("transferencia-nao-encontrada", "Transferência não encontrada.", TipoErro.NaoEncontrado);
}
