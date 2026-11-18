using System.Net;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Idempotência por pagador com Idempotency-Key — US2 da spec 006 (FR-010, FR-011; SC-004).</summary>
public class IdempotenciaTransferenciaTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReenvioSequencial_200MesmoComprovanteUmDebito()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();

        using var primeira = await TransferirAsync(client, pf, pj.Documento, 3m, chave);
        using var segunda = await TransferirAsync(client, pf, Dados.CnpjComMascara(pj.Documento), 3m, chave, "descrição diferente");

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (primeira.StatusCode, segunda.StatusCode));
        var (original, reenvio) = (await ApiBank.LerJsonAsync(primeira), await ApiBank.LerJsonAsync(segunda));
        Assert.Equal(original.GetProperty("transferenciaId").GetGuid(), reenvio.GetProperty("transferenciaId").GetGuid());
        // Spec 008, FR-002a: o reenvio traz o mesmo pagador, com o documento mascarado.
        Assert.Equal(original.GetProperty("pagador").GetRawText(), reenvio.GetProperty("pagador").GetRawText());
        Assert.StartsWith("•••.", reenvio.GetProperty("pagador").GetProperty("documento").GetString(), StringComparison.Ordinal);
        Assert.Equal(7m, await SaldoAsync(client, pf));
    }

    [Fact]
    public async Task MesmaChaveComOutroValorOuOutroDestino_409()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var outra = await CriarPjAsync(client);
        var chave = Guid.NewGuid();
        (await TransferirAsync(client, pf, pj.Documento, 3m, chave)).Dispose();

        using var outroValor = await TransferirAsync(client, pf, pj.Documento, 4m, chave);
        using var outroDestino = await TransferirAsync(client, pf, outra.Documento, 3m, chave);

        await ApiBank.AssertProblemaAsync(outroValor, HttpStatusCode.Conflict, "urn:bank:problema:idempotencia-divergente");
        await ApiBank.AssertProblemaAsync(outroDestino, HttpStatusCode.Conflict, "urn:bank:problema:idempotencia-divergente");
        Assert.Equal(7m, await SaldoAsync(client, pf));
    }

    [Fact]
    public async Task VintePararelasComAMesmaChave_UmaTransferenciaEUmDebito()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();

        var status = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var resposta = await TransferirAsync(client, pf, pj.Documento, 2m, chave);
            return resposta.StatusCode;
        }));

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(19, status.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(8m, await SaldoAsync(client, pf));
        await using var contexto = factory.CriarContexto();
        Assert.Equal(1, await contexto.Transferencias.CountAsync(t => t.CarteiraOrigemId == pf.CarteiraId, Ct));
    }

    [Fact]
    public async Task DezParalelasMesmaChaveValoresDiferentes_Uma201EDemais409()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();

        var status = await Task.WhenAll(Enumerable.Range(1, 10).Select(async i =>
        {
            using var resposta = await TransferirAsync(client, pf, pj.Documento, i * 0.10m, chave);
            return resposta.StatusCode;
        }));

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(9, status.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task MesmaChavePorOutroPagador_NovaTransferencia()
    {
        using var client = factory.CreateClient();
        var a = await CriarPfAsync(client);
        var b = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();

        using var deA = await TransferirAsync(client, a, pj.Documento, 1m, chave);
        using var deB = await TransferirAsync(client, b, pj.Documento, 1m, chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (deA.StatusCode, deB.StatusCode));
    }

    [Fact]
    public async Task ReplayComSaldoJaInsuficiente_200ComOOriginal()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();
        (await TransferirAsync(client, pf, pj.Documento, 10m, chave)).Dispose();

        using var replay = await TransferirAsync(client, pf, pj.Documento, 10m, chave);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(0m, await SaldoAsync(client, pf));
    }
}
