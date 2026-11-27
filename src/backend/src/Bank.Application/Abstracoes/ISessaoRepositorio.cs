using Bank.Domain.Sessoes;

namespace Bank.Application.Abstracoes;

/// <summary>
/// Persistência do agregado <see cref="Sessao"/> (specs/016-sessao-refresh-token, research R2, R3). A gravação é feita
/// por <see cref="IUnidadeDeTrabalho"/>.
/// </summary>
public interface ISessaoRepositorio
{
    /// <summary>
    /// Sessão dona da credencial (ativa ou já trocada), bloqueada até o fim da transação, com a credencial
    /// apresentada e a ativa carregadas; <c>null</c> se o hash não existe. Exige transação em andamento.
    /// </summary>
    Task<Sessao?> ObterPorCredencialComBloqueioAsync(HashCredencial hash, CancellationToken cancellationToken);

    void Adicionar(Sessao sessao);
}

/// <summary>Uma credencial recém-gerada: o valor vai só para o cookie; o hash, para o banco.</summary>
public sealed record CredencialGerada(string Valor, HashCredencial Hash);

/// <summary>Geração e hash das credenciais de renovação (research R1).</summary>
public interface IGeradorCredencial
{
    CredencialGerada Gerar();

    /// <summary>Hash de um valor recebido; um valor malformado resulta num hash que não existe no banco.</summary>
    HashCredencial CalcularHash(string valor);
}

/// <summary>Remoção das sessões encerradas há mais que a retenção (FR-013; research R8).</summary>
public interface ILimpezaSessoes
{
    /// <summary>Remove sessões revogadas ou vencidas antes de <paramref name="limite"/>, com as credenciais.</summary>
    Task<int> RemoverEncerradasAntesDeAsync(DateTimeOffset limite, TimeSpan inatividade, CancellationToken cancellationToken);
}
