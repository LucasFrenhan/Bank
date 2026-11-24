using Bank.Domain.Cartoes;

namespace Bank.Application.Abstracoes;

/// <summary>Fatura ainda não liberada e as compras que faltam lançar nela.</summary>
public sealed record FaturaAProcessar(Fatura Fatura, IReadOnlyList<CompraCartao> ComprasALancar);

/// <summary>
/// Persistência de faturas, sempre com o cartão bloqueado (specs/014-cartao-credito-cobrancas, US3 a US5;
/// research R5, R7).
/// </summary>
public interface IFaturaRepositorio
{
    /// <summary>Fatura da competência; abre uma nova se ainda não existir.</summary>
    Task<Fatura> ObterOuAbrirAsync(CartaoId cartaoId, CompetenciaFatura competencia, DateTimeOffset agora, CancellationToken cancellationToken);

    Task<Fatura?> ObterAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken);

    /// <summary>Competência da fatura paga com a chave, se houver.</summary>
    Task<CompetenciaFatura?> ObterCompetenciaPorChaveAsync(CartaoId cartaoId, Guid chaveIdempotencia, CancellationToken cancellationToken);

    /// <summary>Há fatura liberada, não paga e vencida antes de hoje (FR-016).</summary>
    Task<bool> ExisteVencidaAsync(CartaoId cartaoId, DateOnly hoje, CancellationToken cancellationToken);

    /// <summary>Cartões com compra de dia anterior a lançar ou fatura fechada a liberar (FR-022).</summary>
    Task<IReadOnlyList<CartaoId>> ListarCartoesAProcessarAsync(DateOnly hoje, CancellationToken cancellationToken);

    Task<IReadOnlyList<FaturaAProcessar>> ListarNaoLiberadasAsync(CartaoId cartaoId, CancellationToken cancellationToken);

    void Adicionar(LancamentoFatura lancamento);
}
