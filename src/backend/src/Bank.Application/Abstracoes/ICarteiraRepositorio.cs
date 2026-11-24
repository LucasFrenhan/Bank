using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;

namespace Bank.Application.Abstracoes;

/// <summary>Movimentação de uma única carteira, dentro de uma <see cref="IUnidadeDeTrabalho"/> (specs/014-cartao-credito-cobrancas, FR-034).</summary>
public interface ICarteiraRepositorio
{
    Task<Carteira?> BloquearAsync(CarteiraId carteiraId, CancellationToken cancellationToken);

    void Adicionar(Lancamento lancamento);
}
