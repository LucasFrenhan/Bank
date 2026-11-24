using Bank.Domain.Cartoes;

namespace Bank.Application.Abstracoes;

public sealed record ItemFatura(string Estabelecimento, string Descricao, decimal Valor, DateOnly DataCompra);

/// <summary>Leitura de faturas (specs/014-cartao-credito-cobrancas, US4).</summary>
public interface IFaturaConsultas
{
    Task<IReadOnlyList<Fatura>> ListarAsync(CartaoId cartaoId, CancellationToken cancellationToken);

    Task<Fatura?> ObterAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken);

    /// <summary>Lançamentos por data da compra, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<ItemFatura>> ListarLancamentosAsync(FaturaId faturaId, CancellationToken cancellationToken);
}
