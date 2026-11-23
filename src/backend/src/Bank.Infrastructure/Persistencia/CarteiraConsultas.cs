using Bank.Application.Abstracoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Leitura da carteira de um usuário, sempre com o saldo atual da base (FR-022), e os dados
/// bancários com o nome do banco vindo da tabela <c>bancos</c> (specs/012-dados-bancarios-carteira,
/// FR-009; research R5).
/// </summary>
internal sealed class CarteiraConsultas(BankDbContext contexto) : ICarteiraConsultas
{
    public Task<DadosCarteira?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken) =>
        contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Join(
                contexto.Bancos,
                u => u.Carteira.CodigoBanco,
                b => b.Codigo,
                (u, b) => new DadosCarteira(
                    u.Carteira.Id,
                    u.Id,
                    u.NomeCompleto,
                    u.TipoConta,
                    u.Carteira.Saldo,
                    u.Carteira.Moeda,
                    u.Carteira.AtualizadoEm,
                    new DadosContaBancaria(
                        u.Carteira.CodigoBanco,
                        b.Nome,
                        u.Carteira.Agencia,
                        u.Carteira.NumeroConta,
                        u.Carteira.DigitoConta,
                        u.Carteira.TipoContaBancaria)))
            .SingleOrDefaultAsync(cancellationToken);
}
