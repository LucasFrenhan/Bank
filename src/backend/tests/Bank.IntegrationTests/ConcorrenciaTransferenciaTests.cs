using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Fábrica com saldo inicial PF de R$ 1.000,00: a abertura fica consistente com o saldo, sem
/// editar lançamentos (specs/006-transferencias, research R11).
/// </summary>
public sealed class SaldoMilApiFactory : BankApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Carteiras:SaldoInicial:PessoaFisica"] = "1000.00" }));
    }
}

/// <summary>US3 da spec 006: bloqueio ordenado sem gasto duplo nem impasse (SC-002, SC-003, SC-005).</summary>
public class ConcorrenciaTransferenciaTests(SaldoMilApiFactory factory) : IClassFixture<SaldoMilApiFactory>
{
    [Fact]
    public async Task CincoDe300ComSaldoMil_Exatamente3Concluidas_Repetido5Vezes()
    {
        using var client = factory.CreateClient();
        var destino = await CriarPjAsync(client);

        for (var rodada = 0; rodada < 5; rodada++)
        {
            var pagador = await CriarPfAsync(client);
            Assert.Equal(1000m, await SaldoAsync(client, pagador));

            var status = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
            {
                using var resposta = await TransferirAsync(client, pagador, destino.Documento, 300m);
                if (resposta.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    Assert.Equal("urn:bank:problema:saldo-insuficiente", (await ApiBank.LerJsonAsync(resposta)).GetProperty("type").GetString());
                }

                return resposta.StatusCode;
            }));

            Assert.Equal(3, status.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(2, status.Count(s => s == HttpStatusCode.UnprocessableEntity));
            Assert.Equal(100m, await SaldoAsync(client, pagador));
            Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pagador.CarteiraId));
        }

        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, destino.CarteiraId));
    }

    [Fact]
    public async Task CinquentaCruzadasSimultaneas_TerminamSemImpasseEPreservamOTotal()
    {
        using var client = factory.CreateClient();
        var a = await CriarPfAsync(client);
        var b = await CriarPfAsync(client);
        var cronometro = Stopwatch.StartNew();

        var status = await Task.WhenAll(Enumerable.Range(0, 50).Select(async i =>
        {
            var (de, para) = i % 2 == 0 ? (a, b) : (b, a);
            using var resposta = await TransferirAsync(client, de, para.Documento, 1.25m);
            return resposta.StatusCode;
        }));

        cronometro.Stop();
        Assert.All(status, s => Assert.Equal(HttpStatusCode.Created, s));
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(10), $"levou {cronometro.Elapsed}");
        Assert.Equal(2000m, await SaldoAsync(client, a) + await SaldoAsync(client, b));
        Assert.Equal((1000m, 1000m), (await SaldoAsync(client, a), await SaldoAsync(client, b)));
        Assert.Equal((0m, 0m), (await DiferencaSaldoRazaoAsync(factory, a.CarteiraId), await DiferencaSaldoRazaoAsync(factory, b.CarteiraId)));
    }
}
