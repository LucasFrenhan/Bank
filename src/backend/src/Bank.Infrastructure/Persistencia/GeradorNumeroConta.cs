using Bank.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Próximo número de conta pela sequence <c>seq_numero_conta_one_bank</c>
/// (specs/012-dados-bancarios-carteira, research R2; SQL em <c>Sql/Consultas</c>).
/// </summary>
internal sealed class GeradorNumeroConta(BankDbContext contexto) : IGeradorNumeroConta
{
    public const string ArquivoSql = "Consultas/ObterProximoNumeroConta.sql";

    private static readonly string Sql = SqlEmbutido.Ler(ArquivoSql);

    public async Task<long> ProximoAsync(CancellationToken cancellationToken) =>
        await contexto.Database.SqlQueryRaw<long>(Sql).SingleAsync(cancellationToken);
}
