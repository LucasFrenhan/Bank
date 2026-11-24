using Bank.Application.Abstracoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura dos recebíveis do lojista (specs/014-cartao-credito-cobrancas, FR-033).</summary>
internal sealed class RecebivelConsultas(BankDbContext contexto) : IRecebivelConsultas
{
    public Task<DadosRecebivel?> ObterAsync(UsuarioId lojista, RecebivelId recebivelId, CancellationToken cancellationToken) =>
        Projetar(contexto.Recebiveis.Where(r => r.Id == recebivelId && r.LojistaUsuarioId == lojista)).SingleOrDefaultAsync(cancellationToken);

    public async Task<Pagina<DadosRecebivel>> ListarAsync(UsuarioId lojista, SituacaoRecebivel? situacao, int deslocamento, int quantidade, CancellationToken cancellationToken)
    {
        var consulta = contexto.Recebiveis.Where(r => r.LojistaUsuarioId == lojista);
        if (situacao is { } filtro)
        {
            consulta = consulta.Where(r => r.Situacao == filtro);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var pagina = consulta.OrderByDescending(r => r.CriadoEm).ThenByDescending(r => r.Id).Skip(deslocamento).Take(quantidade);
        var itens = await Projetar(pagina).ToListAsync(cancellationToken);

        return new Pagina<DadosRecebivel>([.. itens.OrderByDescending(i => i.CriadoEm).ThenByDescending(i => i.RecebivelId.Valor)], total);
    }

    private IQueryable<DadosRecebivel> Projetar(IQueryable<Recebivel> recebiveis) =>
        from r in recebiveis.AsNoTracking()
        join cobranca in contexto.Cobrancas on r.CobrancaId equals cobranca.Id
        select new DadosRecebivel(
            r.Id, r.CobrancaId, cobranca.Descricao, r.Situacao, r.ValorBruto, r.Taxa, r.ValorLiquido, r.DataPrevista, r.RecebidoEm, r.CriadoEm);
}
