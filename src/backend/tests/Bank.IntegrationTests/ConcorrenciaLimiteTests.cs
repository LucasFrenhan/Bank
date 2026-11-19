using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Saldo inicial PF de R$ 5.000,00, diário de R$ 1.000,00 e sem limite por operação.</summary>
public sealed class ConcorrenciaLimiteApiFactory : BankApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Carteiras:SaldoInicial:PessoaFisica"] = "5000.00",
            ["Transferencias:Limites:PessoaFisica:PorOperacao"] = string.Empty,
            ["Transferencias:Limites:PessoaFisica:Diario"] = "1000.00",
        }));
    }
}

/// <summary>
/// US3 da spec 009: o limite diário não é furado por envios simultâneos e o replay consome o limite
/// uma única vez (SC-001, SC-002, SC-004).
/// </summary>
public class ConcorrenciaLimiteTests(ConcorrenciaLimiteApiFactory factory) : IClassFixture<ConcorrenciaLimiteApiFactory>
{
    [Fact]
    public async Task CincoDe300ComDiarioMil_Exatamente3Concluidas_Repetido5Vezes()
    {
        using var client = factory.CreateClient();
        var destino = await CriarPjAsync(client);

        for (var rodada = 0; rodada < 5; rodada++)
        {
            var pagador = await CriarPfAsync(client);

            var status = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
            {
                using var resposta = await TransferirAsync(client, pagador, destino.Documento, 300.00m);
                if (resposta.StatusCode == HttpStatusCode.UnprocessableEntity)
                {
                    var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
                    AssertLimite(json, "Diario", limite: 1000.00m, disponivel: 100.00m);
                }

                return resposta.StatusCode;
            }));

            Assert.Equal(3, status.Count(s => s == HttpStatusCode.Created));
            Assert.Equal(2, status.Count(s => s == HttpStatusCode.UnprocessableEntity));
            Assert.Equal(4100.00m, await SaldoAsync(client, pagador));
            Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pagador.CarteiraId));
        }
    }

    [Fact]
    public async Task VinteReenviosParalelosDaMesmaChave_ConsomemOLimiteUmaVez()
    {
        using var client = factory.CreateClient();
        var (pagador, destino) = (await CriarPfAsync(client), await CriarPjAsync(client));
        var chave = Guid.NewGuid();

        var status = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var resposta = await TransferirAsync(client, pagador, destino.Documento, 600.00m, chave);
            return resposta.StatusCode;
        }));

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(19, status.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(4400.00m, await SaldoAsync(client, pagador));

        // Sobram R$ 400,00 do diário: o replay não consumiu limite de novo.
        using var cabe = await TransferirAsync(client, pagador, destino.Documento, 400.00m);
        Assert.Equal(HttpStatusCode.Created, cabe.StatusCode);
    }
}
