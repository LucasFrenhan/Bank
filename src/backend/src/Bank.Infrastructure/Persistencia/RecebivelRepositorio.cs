using Bank.Application.Abstracoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Recebíveis (specs/014-cartao-credito-cobrancas, US6; research R8).</summary>
internal sealed class RecebivelRepositorio(BankDbContext contexto) : IRecebivelRepositorio
{
    public const string ArquivoBloqueio = "Consultas/BloquearRecebivel.sql";

    public async Task<RecebivelBloqueado?> BloquearAsync(RecebivelId recebivelId, CancellationToken cancellationToken)
    {
        var recebiveis = await contexto.Recebiveis
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoBloqueio), new NpgsqlParameter("id", recebivelId.Valor))
            .ToListAsync(cancellationToken);
        if (recebiveis.SingleOrDefault() is not { } recebivel)
        {
            return null;
        }

        var descricao = await contexto.Cobrancas.AsNoTracking()
            .Where(c => c.Id == recebivel.CobrancaId)
            .Select(c => c.Descricao)
            .SingleAsync(cancellationToken);
        return new RecebivelBloqueado(recebivel, descricao);
    }

    public Task<RecebivelId?> ObterPorChaveAsync(UsuarioId lojista, Guid chaveIdempotencia, CancellationToken cancellationToken) =>
        contexto.Recebiveis.AsNoTracking()
            .Where(r => r.LojistaUsuarioId == lojista && r.ChaveIdempotencia == chaveIdempotencia)
            .Select(r => (RecebivelId?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RecebivelId>> ListarDevidosAsync(DateOnly hoje, CancellationToken cancellationToken) =>
        await contexto.Recebiveis.AsNoTracking()
            .Where(r => r.Situacao == SituacaoRecebivel.Pendente && r.DataPrevista <= hoje)
            .OrderBy(r => r.DataPrevista)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
}
