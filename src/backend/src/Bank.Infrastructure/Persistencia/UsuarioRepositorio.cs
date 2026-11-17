using Bank.Application.Abstracoes;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Persistência de usuários e carteiras (research R7).</summary>
internal sealed class UsuarioRepositorio(BankDbContext contexto) : IUsuarioRepositorio
{
    public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken) =>
        contexto.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<bool> ExisteDocumentoAsync(string documento, CancellationToken cancellationToken) =>
        contexto.Usuarios.AnyAsync(u => u.Documento == documento, cancellationToken);

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken) =>
        contexto.Usuarios.Include(u => u.Carteira).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<ResultadoPersistencia> AdicionarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        // Usuário e carteira (navegação) são gravados no mesmo SaveChanges: uma transação (RN-04).
        contexto.Usuarios.Add(usuario);

        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
            return ResultadoPersistencia.Ok;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violacao)
        {
            contexto.ChangeTracker.Clear();

            return violacao.ConstraintName switch
            {
                UsuarioConfiguracao.IndiceEmail => ResultadoPersistencia.EmailDuplicado,
                UsuarioConfiguracao.IndiceDocumento => ResultadoPersistencia.DocumentoDuplicado,
                _ => throw new InvalidOperationException("Violação de unicidade inesperada ao cadastrar usuário.", ex),
            };
        }
    }

    public Task AtualizarAsync(Usuario usuario, CancellationToken cancellationToken) =>
        contexto.SaveChangesAsync(cancellationToken);
}
