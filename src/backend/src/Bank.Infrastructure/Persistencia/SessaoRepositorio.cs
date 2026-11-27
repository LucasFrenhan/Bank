using Bank.Application.Abstracoes;
using Bank.Domain.Sessoes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Sessões e credenciais de renovação (specs/016-sessao-refresh-token, research R2, R3, R8). A busca bloqueia a
/// sessão com <c>FOR UPDATE</c> e carrega só a credencial apresentada e a ativa.
/// </summary>
internal sealed class SessaoRepositorio(BankDbContext contexto) : ISessaoRepositorio, ILimpezaSessoes
{
    public const string ArquivoBloqueio = "Consultas/BloquearSessao.sql";

    public async Task<Sessao?> ObterPorCredencialComBloqueioAsync(HashCredencial hash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var parametro = new NpgsqlParameter("hash", NpgsqlDbType.Bytea) { Value = hash.Bytes };
        // Sem composição sobre o SQL (o FOR UPDATE fica na consulta externa).
        var bloqueadas = await contexto.Sessoes
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoBloqueio), parametro)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (bloqueadas is not [var bloqueada])
        {
            return null;
        }

        return await contexto.Sessoes
            .Include(s => s.Credenciais.Where(c => c.RotacionadaEm == null || c.Hash == hash))
            .SingleAsync(s => s.Id == bloqueada.Id, cancellationToken);
    }

    public void Adicionar(Sessao sessao) => contexto.Sessoes.Add(sessao);

    public Task<int> RemoverEncerradasAntesDeAsync(DateTimeOffset limite, TimeSpan inatividade, CancellationToken cancellationToken) =>
        contexto.Sessoes
            .Where(s => (s.RevogadaEm != null && s.RevogadaEm < limite)
                || s.ExpiraEm < limite
                || s.UltimaRenovacaoEm + inatividade < limite)
            .ExecuteDeleteAsync(cancellationToken);
}
