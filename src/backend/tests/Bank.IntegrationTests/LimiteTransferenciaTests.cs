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

    // ---------- GET /limites (US4; FR-012) ----------

    private const string CaminhoLimites = "/api/v1/transferencias/limites";

    [Fact]
    public async Task Limites_PF_200AntesEDepoisDeTransferir()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using var antes = await GetComTokenAsync(client, pf.Token, CaminhoLimites);
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        var json = await ApiBank.LerJsonAsync(antes);
        Assert.Equal((1000.00m, 2000.00m, 0m, 2000.00m), (
            json.GetProperty("porOperacao").GetDecimal(),
            json.GetProperty("diario").GetDecimal(),
            json.GetProperty("transferidoHoje").GetDecimal(),
            json.GetProperty("disponivelHoje").GetDecimal()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", json.GetProperty("dia").GetString());

        using (var transferencia = await TransferirAsync(client, pf, pj.Documento, 750.00m))
        {
            Assert.Equal(HttpStatusCode.Created, transferencia.StatusCode);
        }

        using var depois = await GetComTokenAsync(client, pf.Token, CaminhoLimites);
        var jsonDepois = await ApiBank.LerJsonAsync(depois);
        Assert.Equal((750.00m, 1250.00m), (jsonDepois.GetProperty("transferidoHoje").GetDecimal(), jsonDepois.GetProperty("disponivelHoje").GetDecimal()));
    }

    [Theory]
    [InlineData("sem-token")]
    [InlineData("adulterado")]
    public async Task Limites_SemTokenOuTokenInvalido_401(string cenario)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());

        using var resposta = cenario == "sem-token"
            ? await client.GetAsync(new Uri(CaminhoLimites, UriKind.Relative), TestContext.Current.CancellationToken)
            : await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims)[..^4] + "AAAA", CaminhoLimites);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }

    [Fact]
    public async Task Limites_PJ_403()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        using var resposta = await GetComTokenAsync(client, pj.Token, CaminhoLimites);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }
}
