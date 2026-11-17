using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Imutabilidade de transferências e lançamentos e invariante saldo = razão (spec 006, FR-015; SC-005).</summary>
public class RazaoEImutabilidadeTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // SQL só de teste (isento da regra de arquivos .sql — constituição v1.1.0).
    public static TheoryData<string> Comandos() => new()
    {
        "UPDATE lancamentos SET valor = valor + 1",
        "DELETE FROM lancamentos",
        "UPDATE transferencias SET descricao = 'alterada'",
        "DELETE FROM transferencias",
    };

    [Theory]
    [MemberData(nameof(Comandos))]
    public async Task AlterarOuApagar_FalhaPelaTrigger(string comando)
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        (await TransferirAsync(client, pf, pj.Documento, 1m)).Dispose();

        await using var contexto = factory.CriarContexto();
        var erro = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(comando, Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, erro.SqlState);
    }

    [Fact]
    public async Task AposTransferenciasVariadas_TodaCarteiraTemSaldoIgualAoRazao()
    {
        using var client = factory.CreateClient();
        var a = await CriarPfAsync(client);
        var b = await CriarPfAsync(client);
        var c = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);

        await Task.WhenAll(
            Enviar(a, b.Documento, 3.33m), Enviar(b, c.Documento, 7.01m), Enviar(c, a.Documento, 0.99m),
            Enviar(a, pj.Documento, 2m), Enviar(b, pj.Documento, 50m), Enviar(c, b.Documento, 4.4m),
            Enviar(a, c.Documento, 1.11m), Enviar(b, a.Documento, 0.01m));

        await using var contexto = factory.CriarContexto();
        var divergentes = await contexto.Carteiras
            .Where(cart => cart.Saldo != contexto.Lancamentos
                .Where(l => l.CarteiraId == cart.Id)
                .Sum(l => l.Direcao == Bank.Domain.Transferencias.DirecaoLancamento.Entrada ? l.Valor : -l.Valor))
            .CountAsync(Ct);
        Assert.Equal(0, divergentes);

        async Task Enviar(ContaTeste de, string para, decimal valor) => (await TransferirAsync(client, de, para, valor)).Dispose();
    }
}
