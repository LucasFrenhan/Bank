using Bank.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura dos dados do titular, sempre atual e sem senha nem carteira (specs/005-usuarios-me, R1).</summary>
internal sealed class UsuarioConsultas(BankDbContext contexto) : IUsuarioConsultas
{
    public Task<DadosTitular?> ObterTitularAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new DadosTitular(u.Id, u.NomeCompleto, u.Email, u.Documento, u.TipoConta, u.CriadoEm))
            .SingleOrDefaultAsync(cancellationToken);
}
