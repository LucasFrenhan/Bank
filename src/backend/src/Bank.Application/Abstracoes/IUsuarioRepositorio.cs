using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Resultado da persistência de um novo usuário (unicidade garantida pela base — FR-008).</summary>
public enum ResultadoPersistencia
{
    Ok,
    EmailDuplicado,
    DocumentoDuplicado,
}

/// <summary>Persistência do agregado <see cref="Usuario"/> (implementada em Infrastructure).</summary>
public interface IUsuarioRepositorio
{
    Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ExisteDocumentoAsync(string documento, CancellationToken cancellationToken);

    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Usuário com a carteira, para emitir o token na renovação (specs/016-sessao-refresh-token).</summary>
    Task<Usuario?> ObterPorIdAsync(UsuarioId usuarioId, CancellationToken cancellationToken);

    /// <summary>Persiste usuário e carteira em uma única transação (RN-04).</summary>
    Task<ResultadoPersistencia> AdicionarAsync(Usuario usuario, CancellationToken cancellationToken);

    /// <summary>Grava alterações de um usuário obtido por este repositório (ex.: rehash da senha).</summary>
    Task AtualizarAsync(Usuario usuario, CancellationToken cancellationToken);
}
