using Bank.Domain.Comum;

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
        new("idempotencia-divergente", "Esta chave de idempotência já foi usada com outro destinatário ou valor.", TipoErro.Conflito);
}
