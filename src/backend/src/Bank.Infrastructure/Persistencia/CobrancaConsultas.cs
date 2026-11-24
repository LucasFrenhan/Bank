using Bank.Application.Abstracoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura de cobranças (specs/014-cartao-credito-cobrancas, FR-011, FR-012).</summary>
internal sealed class CobrancaConsultas(BankDbContext contexto) : ICobrancaConsultas
{
    public Task<DadosCobranca?> ObterAsync(CobrancaId cobrancaId, CancellationToken cancellationToken) =>
        Projetar(contexto.Cobrancas.Where(c => c.Id == cobrancaId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<Pagina<DadosCobranca>> ListarDoLojistaAsync(UsuarioId lojista, StatusCobranca? status, int deslocamento, int quantidade, CancellationToken cancellationToken)
    {
        var consulta = contexto.Cobrancas.Where(c => c.LojistaUsuarioId == lojista);
        if (status is { } filtro)
        {
            consulta = consulta.Where(c => c.Status == filtro);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var pagina = consulta.OrderByDescending(c => c.CriadoEm).ThenByDescending(c => c.Id).Skip(deslocamento).Take(quantidade);
        var itens = await Projetar(pagina).ToListAsync(cancellationToken);

        return new Pagina<DadosCobranca>([.. itens.OrderByDescending(i => i.CriadoEm).ThenByDescending(i => i.CobrancaId.Valor)], total);
    }

    private IQueryable<DadosCobranca> Projetar(IQueryable<Cobranca> cobrancas) =>
        from c in cobrancas.AsNoTracking()
        join lojista in contexto.Usuarios on c.LojistaUsuarioId equals lojista.Id
        select new DadosCobranca(
            c.Id, c.LojistaUsuarioId, lojista.NomeCompleto, lojista.Documento, c.Valor, c.Descricao, c.Status, c.CriadoEm, c.PagaEm, c.CanceladaEm,
            c.PagadorUsuarioId, c.CodigoBandeira, c.UltimosDigitosCartao, c.CodigoAutorizacao, c.UltimaRecusaMotivo, c.UltimaRecusaEm);
}
