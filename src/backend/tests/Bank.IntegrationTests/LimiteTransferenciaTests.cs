using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Fábrica da spec 009: saldo inicial PF de R$ 5.000,00, limite por operação de R$ 1.000,00 e
/// diário de R$ 2.000,00.
/// </summary>
public sealed class LimitesApiFactory : BankApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Carteiras:SaldoInicial:PessoaFisica"] = "5000.00",
            ["Transferencias:Limites:PessoaFisica:PorOperacao"] = "1000.00",
            ["Transferencias:Limites:PessoaFisica:Diario"] = "2000.00",
        }));
    }
}

/// <summary>
/// Limite por operação, replay e consulta de limites (specs/009-limite-transferencia: US1, US3,
/// US4; contracts/transferencias-api.md v1.2.0).
/// </summary>
public class LimiteTransferenciaTests(LimitesApiFactory factory) : IClassFixture<LimitesApiFactory>
{
    [Fact]
    public async Task PorOperacao_ValorIgualAoLimite_201()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using var resposta = await TransferirAsync(client, pf, pj.Documento, 1000.00m);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task PorOperacao_UmCentavoAcima_422ComExtensoesESaldosInalterados()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using var resposta = await TransferirAsync(client, pf, pj.Documento, 1000.01m);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
        AssertLimite(json, "PorOperacao", limite: 1000.00m, disponivel: 1000.00m);
        Assert.Equal("Limite de transferência excedido", json.GetProperty("title").GetString());
        Assert.Equal((5000.00m, 0m), (await SaldoAsync(client, pf), await SaldoAsync(client, pj)));
    }

    [Fact]
    public async Task PorOperacao_AcimaDoLimiteEDoSaldo_422LimiteExcedido()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using var resposta = await TransferirAsync(client, pf, pj.Documento, 6000.00m);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
    }

    [Fact]
    public async Task Replay_ComLimiteDiarioEsgotado_200ComOOriginal()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));
        var chave = Guid.NewGuid();

        using var original = await TransferirAsync(client, pf, pj.Documento, 1000.00m, chave);
        using var esgota = await TransferirAsync(client, pf, pj.Documento, 1000.00m);
        using var replay = await TransferirAsync(client, pf, pj.Documento, 1000.00m, chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.OK), (original.StatusCode, esgota.StatusCode, replay.StatusCode));
        Assert.Equal(
            (await ApiBank.LerJsonAsync(original)).GetProperty("transferenciaId").GetGuid(),
            (await ApiBank.LerJsonAsync(replay)).GetProperty("transferenciaId").GetGuid());
        Assert.Equal(3000.00m, await SaldoAsync(client, pf));
    }

    [Fact]
    public async Task Replay_DepoisDeReduzirOLimite_ContinuaComOOriginal()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));
        var chave = Guid.NewGuid();
        using var original = await TransferirAsync(client, pf, pj.Documento, 1000.00m, chave);

        await using var reduzida = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Transferencias:Limites:PessoaFisica:PorOperacao"] = "500.00" })));
        using var clientReduzido = reduzida.CreateClient();
        using var replay = await TransferirAsync(clientReduzido, pf, pj.Documento, 1000.00m, chave);
        using var nova = await TransferirAsync(clientReduzido, pf, pj.Documento, 1000.00m);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nova.StatusCode);
    }

    [Fact]
    public async Task Recusa_NaoGravaAChave_MesmaChaveDepoisCom500_201()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));
        var chave = Guid.NewGuid();

        using var recusada = await TransferirAsync(client, pf, pj.Documento, 1500.00m, chave);
        using var concluida = await TransferirAsync(client, pf, pj.Documento, 500.00m, chave);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, recusada.StatusCode);
        Assert.Equal(HttpStatusCode.Created, concluida.StatusCode);
    }
}
