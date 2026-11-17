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

/// <summary>Titular de destino de uma transferência, encontrado pelo documento (spec 006, FR-002).</summary>
public sealed record Destinatario(Guid UsuarioId, Guid CarteiraId, string Nome, string Documento, TipoConta TipoConta);

/// <summary>Consultas de leitura de usuários (implementadas em Infrastructure).</summary>
public interface IUsuarioConsultas
{
    Task<DadosTitular?> ObterTitularAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <param name="documento">Documento já normalizado (sem máscara, maiúsculas).</param>
    Task<Destinatario?> ObterDestinatarioAsync(string documento, CancellationToken cancellationToken);
}
