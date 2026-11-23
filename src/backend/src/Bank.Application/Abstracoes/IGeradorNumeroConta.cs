namespace Bank.Application.Abstracoes;

/// <summary>
/// Próximo número de conta do banco 001, de um contador único e sequencial
/// (specs/012-dados-bancarios-carteira, FR-008; research R2). Cadastros simultâneos nunca recebem
/// o mesmo número; o número de um cadastro que falha fica sem uso.
/// </summary>
public interface IGeradorNumeroConta
{
    Task<long> ProximoAsync(CancellationToken cancellationToken);
}
