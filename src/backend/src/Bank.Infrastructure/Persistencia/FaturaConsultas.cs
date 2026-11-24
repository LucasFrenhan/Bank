using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura de faturas (specs/014-cartao-credito-cobrancas, FR-027).</summary>
internal sealed class FaturaConsultas(BankDbContext contexto) : IFaturaConsultas
{
    public async Task<IReadOnlyList<Fatura>> ListarAsync(CartaoId cartaoId, CancellationToken cancellationToken) =>
        await contexto.Faturas.AsNoTracking()
            .Where(f => f.CartaoId == cartaoId)
            .OrderByDescending(f => f.Competencia)
            .ToListAsync(cancellationToken);

    public Task<Fatura?> ObterAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken)
    {
        var mes = competencia.PrimeiroDiaDoMes;
        return contexto.Faturas.AsNoTracking().SingleOrDefaultAsync(f => f.CartaoId == cartaoId && f.Competencia == mes, cancellationToken);
    }

    public async Task<IReadOnlyList<ItemFatura>> ListarLancamentosAsync(FaturaId faturaId, CancellationToken cancellationToken) =>
        await contexto.LancamentosFatura.AsNoTracking()
            .Where(l => l.FaturaId == faturaId)
            .OrderByDescending(l => l.DataCompra)
            .ThenByDescending(l => l.Id)
            .Select(l => new ItemFatura(l.Estabelecimento, l.Descricao, l.Valor, l.DataCompra))
            .ToListAsync(cancellationToken);
}
