using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

public sealed record ContraparteExtrato(string Nome, string Documento, TipoConta TipoConta);

/// <summary>Lançamento da carteira sob a ótica do extrato (spec 006, FR-012).</summary>
public sealed record ItemExtrato(
    Guid LancamentoId,
    TipoLancamento Tipo,
    DirecaoLancamento Direcao,
    Guid? TransferenciaId,
    ContraparteExtrato? Contraparte,
    decimal Valor,
    string? Descricao,
    DateTimeOffset CriadoEm);

public sealed record PaginaExtrato(IReadOnlyList<ItemExtrato> Itens, int TotalItens);

/// <summary>Leitura paginada do razão de uma carteira (implementada em Infrastructure).</summary>
public interface IExtratoConsultas
{
    /// <summary>Mais recente primeiro; o total respeita o filtro de direção (FR-013).</summary>
    Task<PaginaExtrato> ObterAsync(Guid carteiraId, DirecaoLancamento? direcao, int deslocamento, int quantidade, CancellationToken cancellationToken);
}
