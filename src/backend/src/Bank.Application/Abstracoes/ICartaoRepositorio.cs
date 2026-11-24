using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Persistência de cartões (specs/014-cartao-credito-cobrancas, US1).</summary>
public interface ICartaoRepositorio
{
    /// <summary>Cartão do usuário, só para leitura.</summary>
    Task<Cartao?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken);

    /// <summary>Bloqueia o cartão; exige transação.</summary>
    Task<Cartao?> BloquearAsync(CartaoId cartaoId, CancellationToken cancellationToken);

    void Adicionar(Cartao cartao);

    /// <summary>Total das compras autorizadas ainda não lançadas em fatura (FR-007).</summary>
    Task<decimal> SomarComprasALancarAsync(CartaoId cartaoId, CancellationToken cancellationToken);
}
