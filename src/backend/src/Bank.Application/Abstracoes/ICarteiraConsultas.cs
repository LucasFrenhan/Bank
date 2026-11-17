using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Dados de leitura da carteira de um usuário.</summary>
public sealed record DadosCarteira(
    Guid CarteiraId,
    Guid UsuarioId,
    string NomeTitular,
    TipoConta TipoConta,
    decimal Saldo,
    string Moeda,
    DateTimeOffset AtualizadoEm);

/// <summary>Consultas de leitura de carteiras (implementadas em Infrastructure).</summary>
public interface ICarteiraConsultas
{
    Task<DadosCarteira?> ObterDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken);
}
