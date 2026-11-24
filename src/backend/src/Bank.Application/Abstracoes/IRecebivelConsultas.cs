using Bank.Domain.Cobrancas;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

public sealed record DadosRecebivel(
    RecebivelId RecebivelId,
    CobrancaId CobrancaId,
    string Descricao,
    SituacaoRecebivel Situacao,
    decimal ValorBruto,
    decimal Taxa,
    decimal ValorLiquido,
    DateOnly DataPrevista,
    DateTimeOffset? RecebidoEm,
    DateTimeOffset CriadoEm);

/// <summary>Leitura dos recebíveis do lojista (specs/014-cartao-credito-cobrancas, US6).</summary>
public interface IRecebivelConsultas
{
    /// <summary>Nulo se inexistente ou de outro lojista.</summary>
    Task<DadosRecebivel?> ObterAsync(UsuarioId lojista, RecebivelId recebivelId, CancellationToken cancellationToken);

    Task<Pagina<DadosRecebivel>> ListarAsync(UsuarioId lojista, SituacaoRecebivel? situacao, int deslocamento, int quantidade, CancellationToken cancellationToken);
}
