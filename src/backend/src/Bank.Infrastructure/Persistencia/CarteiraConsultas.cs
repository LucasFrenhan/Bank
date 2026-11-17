using Bank.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura da carteira de um usuário, sempre com o saldo atual da base (FR-022).</summary>
internal sealed class CarteiraConsultas(BankDbContext contexto) : ICarteiraConsultas
{
    public Task<DadosCarteira?> ObterDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new DadosCarteira(
                u.Carteira.Id,
                u.Id,
                u.NomeCompleto,
                u.TipoConta,
                u.Carteira.Saldo,
                u.Carteira.Moeda,
                u.Carteira.AtualizadoEm))
            .SingleOrDefaultAsync(cancellationToken);
}
