using Bank.SharedKernel;

namespace Bank.Domain.Transferencias;

/// <summary>
/// Tipo de limite que recusou uma transferência (specs/009-limite-transferencia). Novos limites
/// (noturno, por modalidade) acrescentam valores aqui, sem mudar o erro (FR-007).
/// </summary>
public enum TipoLimite
{
    /// <summary>Valor máximo de uma transferência.</summary>
    PorOperacao,

    /// <summary>Soma máxima das transferências concluídas enviadas no dia de Brasília.</summary>
    Diario,
}

/// <summary>
/// Limites de transferência de um perfil (specs/009-limite-transferencia, FR-001, FR-004;
/// research R1). <c>null</c> = sem limite. Os limites são inclusivos: valor igual é permitido.
/// </summary>
public sealed record LimitesTransferencia(decimal? PorOperacao, decimal? Diario)
{
    public Resultado VerificarOperacao(decimal valor) =>
        PorOperacao is { } limite && valor > limite
            ? Resultado.Falha(ErrosTransferencia.LimiteExcedido(TipoLimite.PorOperacao, limite, limite))
            : Resultado.Ok();

    public Resultado VerificarDiario(decimal valor, decimal transferidoHoje) =>
        Diario is { } limite && transferidoHoje + valor > limite
            ? Resultado.Falha(ErrosTransferencia.LimiteExcedido(TipoLimite.Diario, limite, Restante(limite, transferidoHoje)))
            : Resultado.Ok();

    /// <summary>Quanto ainda pode ser enviado hoje; <c>null</c> sem limite diário; nunca negativo.</summary>
    public decimal? DisponivelHoje(decimal transferidoHoje) =>
        Diario is { } limite ? Restante(limite, transferidoHoje) : null;

    private static decimal Restante(decimal limite, decimal transferidoHoje) =>
        Dinheiro.NormalizarEscala(Math.Max(0m, limite - transferidoHoje));
}
