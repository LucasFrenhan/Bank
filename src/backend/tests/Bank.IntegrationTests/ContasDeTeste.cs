using Bank.Domain.Bancos;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>
/// Dados bancários para usuários gravados direto pelo contexto nos testes (spec 012): usa a
/// sequence real para não colidir com os cadastros feitos pela API. SQL só de teste.
/// </summary>
internal static class ContasDeTeste
{
    public static async Task<ContaBancaria> ProximaCorrenteAsync(BankDbContext contexto, CancellationToken cancellationToken)
    {
        var numero = await contexto.Database
            .SqlQueryRaw<long>("SELECT nextval('" + BankDbContext.SequenceNumeroConta + "') AS \"Value\"")
            .SingleAsync(cancellationToken);
        return ContaBancaria.Nova(Banco.OneBank, ContaBancaria.AgenciaPadrao, numero, TipoContaBancaria.ContaCorrente);
    }
}
