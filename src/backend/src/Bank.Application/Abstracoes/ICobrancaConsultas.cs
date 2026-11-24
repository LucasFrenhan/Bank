using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Cobrança com o lojista; a Application decide o que cada pessoa pode ver (FR-011).</summary>
public sealed record DadosCobranca(
    CobrancaId CobrancaId,
    UsuarioId LojistaUsuarioId,
    string LojistaNome,
    string LojistaDocumento,
    decimal Valor,
    string Descricao,
    StatusCobranca Status,
    DateTimeOffset CriadoEm,
    DateTimeOffset? PagaEm,
    DateTimeOffset? CanceladaEm,
    UsuarioId? PagadorUsuarioId,
    string? CodigoBandeira,
    string? UltimosDigitosCartao,
    string? CodigoAutorizacao,
    MotivoRecusa? UltimaRecusaMotivo,
    DateTimeOffset? UltimaRecusaEm);

/// <summary>Página de uma consulta, com o total que respeita os filtros.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, int TotalItens);

/// <summary>Leitura de cobranças (specs/014-cartao-credito-cobrancas, US2).</summary>
public interface ICobrancaConsultas
{
    Task<DadosCobranca?> ObterAsync(CobrancaId cobrancaId, CancellationToken cancellationToken);

    /// <summary>Emitidas pelo lojista, das mais novas para as mais antigas.</summary>
    Task<Pagina<DadosCobranca>> ListarDoLojistaAsync(UsuarioId lojista, StatusCobranca? status, int deslocamento, int quantidade, CancellationToken cancellationToken);
}
