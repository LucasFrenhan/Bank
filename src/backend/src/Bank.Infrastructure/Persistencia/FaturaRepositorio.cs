using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Faturas, com o cartão bloqueado por quem chama (specs/014-cartao-credito-cobrancas, research R5, R7, R8).</summary>
internal sealed class FaturaRepositorio(BankDbContext contexto) : IFaturaRepositorio
{
    public async Task<Fatura> ObterOuAbrirAsync(CartaoId cartaoId, CompetenciaFatura competencia, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var fatura = await ObterAsync(cartaoId, competencia, cancellationToken);
        if (fatura is null)
        {
            fatura = Fatura.Abrir(cartaoId, competencia, agora);
            contexto.Faturas.Add(fatura);
        }

        return fatura;
    }

    public Task<Fatura?> ObterAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken)
    {
        var mes = competencia.PrimeiroDiaDoMes;
        return contexto.Faturas.SingleOrDefaultAsync(f => f.CartaoId == cartaoId && f.Competencia == mes, cancellationToken);
    }

    public async Task<CompetenciaFatura?> ObterCompetenciaPorChaveAsync(CartaoId cartaoId, Guid chaveIdempotencia, CancellationToken cancellationToken)
    {
        var mes = await contexto.Faturas.AsNoTracking()
            .Where(f => f.CartaoId == cartaoId && f.ChaveIdempotencia == chaveIdempotencia)
            .Select(f => (DateOnly?)f.Competencia)
            .SingleOrDefaultAsync(cancellationToken);
        return mes is { } competencia ? CompetenciaFatura.DoMes(competencia) : null;
    }

    public Task<bool> ExisteVencidaAsync(CartaoId cartaoId, DateOnly hoje, CancellationToken cancellationToken) =>
        contexto.Faturas.AnyAsync(f => f.CartaoId == cartaoId && f.LiberadaEm != null && f.PagaEm == null && f.Vencimento < hoje, cancellationToken);

    public async Task<IReadOnlyList<CartaoId>> ListarCartoesAProcessarAsync(DateOnly hoje, CancellationToken cancellationToken) =>
        await contexto.Faturas.AsNoTracking()
            .Where(f => f.LiberadaEm == null
                && (f.Fechamento < hoje
                    || contexto.ComprasCartao.Any(c => c.FaturaId == f.Id && c.DataCompra < hoje && !contexto.LancamentosFatura.Any(l => l.CompraId == c.Id))))
            .Select(f => f.CartaoId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FaturaAProcessar>> ListarNaoLiberadasAsync(CartaoId cartaoId, CancellationToken cancellationToken)
    {
        var faturas = await contexto.Faturas
            .Where(f => f.CartaoId == cartaoId && f.LiberadaEm == null)
            .OrderBy(f => f.Competencia)
            .ToListAsync(cancellationToken);
        var ids = faturas.ConvertAll(f => f.Id);
        var compras = await contexto.ComprasCartao.AsNoTracking()
            .Where(c => ids.Contains(c.FaturaId) && !contexto.LancamentosFatura.Any(l => l.CompraId == c.Id))
            .OrderBy(c => c.AutorizadaEm)
            .ToListAsync(cancellationToken);

        return [.. faturas.Select(f => new FaturaAProcessar(f, compras.FindAll(c => c.FaturaId == f.Id)))];
    }

    public void Adicionar(LancamentoFatura lancamento) => contexto.LancamentosFatura.Add(lancamento);
}
