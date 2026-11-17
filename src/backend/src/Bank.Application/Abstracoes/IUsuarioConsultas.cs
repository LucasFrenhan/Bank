using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Dados cadastrais do titular (specs/005-usuarios-me, data-model). Sem senha nem carteira.</summary>
public sealed record DadosTitular(
    Guid UsuarioId,
    string NomeCompleto,
    string Email,
    string Documento,
    TipoConta TipoConta,
    DateTimeOffset CriadoEm);

/// <summary>Consultas de leitura de usuários (implementadas em Infrastructure).</summary>
public interface IUsuarioConsultas
{
    Task<DadosTitular?> ObterTitularAsync(Guid usuarioId, CancellationToken cancellationToken);
}
