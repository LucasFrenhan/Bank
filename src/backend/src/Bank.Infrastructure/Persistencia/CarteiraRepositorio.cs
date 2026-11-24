using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Bloqueio e lançamento em uma carteira (specs/014-cartao-credito-cobrancas, FR-034; research R8).</summary>
internal sealed class CarteiraRepositorio(BankDbContext contexto) : ICarteiraRepositorio
{
    public async Task<Carteira?> BloquearAsync(CarteiraId carteiraId, CancellationToken cancellationToken)
    {
        Guid[] ids = [carteiraId.Valor];
        var carteiras = await contexto.Carteiras
            .FromSqlRaw(SqlEmbutido.Ler(UnidadeDeTransferencia.ArquivoBloqueio), new NpgsqlParameter("ids", ids))
            .ToListAsync(cancellationToken);
        return carteiras.SingleOrDefault();
    }

    public void Adicionar(Lancamento lancamento) => contexto.Lancamentos.Add(lancamento);
}
