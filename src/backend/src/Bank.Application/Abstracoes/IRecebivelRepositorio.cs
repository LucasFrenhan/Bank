using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Recebível bloqueado, com a descrição da cobrança para o lançamento.</summary>
public sealed record RecebivelBloqueado(Recebivel Recebivel, string DescricaoCobranca);

/// <summary>Persistência de recebíveis (specs/014-cartao-credito-cobrancas, US6).</summary>
public interface IRecebivelRepositorio
{
    /// <summary>Bloqueia o recebível; exige transação.</summary>
    Task<RecebivelBloqueado?> BloquearAsync(RecebivelId recebivelId, CancellationToken cancellationToken);

    /// <summary>Recebível antecipado com a chave, se houver.</summary>
    Task<RecebivelId?> ObterPorChaveAsync(UsuarioId lojista, Guid chaveIdempotencia, CancellationToken cancellationToken);

    /// <summary>Pendentes com data prevista até hoje (FR-031).</summary>
    Task<IReadOnlyList<RecebivelId>> ListarDevidosAsync(DateOnly hoje, CancellationToken cancellationToken);
}
