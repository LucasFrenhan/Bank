using System.Net;
using Bank.Domain.Cobrancas;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Concorrência no pagamento (specs/014-cartao-credito-cobrancas, FR-019, FR-020; SC-003, SC-004).</summary>
public class ConcorrenciaPagamentoTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MesmaCobranca_50Pagamentos_UmAprovado()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartoes = new List<CartaoTeste>();
        for (var i = 0; i < 5; i++)
        {
            cartoes.Add(await CriarPfComCartaoAsync(factory, client));
        }

        var cobranca = await EmitirAsync(client, pj, 100m);

        var status = await StatusAsync(Enumerable.Range(0, 50).Select(i => PagarAsync(client, cartoes[i % 5], cobranca)));

        Assert.Equal((1, 49), (status.Count(s => s == HttpStatusCode.OK), status.Count(s => s == HttpStatusCode.Conflict)));
        var limites = new List<decimal>();
        foreach (var cartao in cartoes)
        {
            limites.Add(await LimiteDisponivelAsync(client, cartao));
        }

        Assert.Equal([4900m, 5000m, 5000m, 5000m, 5000m], limites.Order());
        await using var contexto = factory.CriarContexto();
        Assert.Equal(1, await contexto.Recebiveis.CountAsync(r => r.CobrancaId == CobrancaId.De(cobranca), Ct));
    }

    [Fact]
    public async Task CobrancasQueSomadasExcedemOLimite_NuncaFicaNegativo()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobrancas = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            cobrancas.Add(await EmitirAsync(client, pj, 1500m));
        }

        var status = await StatusAsync(cobrancas.Select(c => PagarAsync(client, cartao, c)));

        Assert.Equal((3, 7), (status.Count(s => s == HttpStatusCode.OK), status.Count(s => s == HttpStatusCode.UnprocessableEntity)));
        Assert.Equal(500m, await LimiteDisponivelAsync(client, cartao));
        await AssertLimiteIgualAsComprasAsync();
    }

    [Fact]
    public async Task MesmaChaveSimultanea_UmEfeito()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj, 100m);
        var chave = Guid.NewGuid();

        var status = await StatusAsync(Enumerable.Range(0, 20).Select(_ => PagarAsync(client, cartao, cobranca, chave)));

        Assert.All(status, s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(4900m, await LimiteDisponivelAsync(client, cartao));
    }

    [Fact]
    public async Task MesmaChaveEmCobrancasDiferentes_UmaAprovada()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var (a, b) = (await EmitirAsync(client, pj, 100m), await EmitirAsync(client, pj, 100m));
        var chave = Guid.NewGuid();

        var status = await StatusAsync([PagarAsync(client, cartao, a, chave), PagarAsync(client, cartao, b, chave)]);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], status.Order());
        Assert.Equal(4900m, await LimiteDisponivelAsync(client, cartao));
    }

    [Fact]
    public async Task CancelarEPagar_UmVence()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);

        for (var i = 0; i < 10; i++)
        {
            var cobranca = await EmitirAsync(client, pj, 1m);

            var status = await StatusAsync([PagarAsync(client, cartao, cobranca), PostAsync(client, pj.Token, $"{Cobrancas}/{cobranca}/cancelar")]);

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], status.Order());
        }

        await AssertLimiteIgualAsComprasAsync();
    }

    private async Task AssertLimiteIgualAsComprasAsync()
    {
        await using var contexto = factory.CriarContexto();
        var divergentes = await contexto.Cartoes.CountAsync(
            c => c.LimiteUtilizado != (contexto.ComprasCartao
                .Where(compra => compra.CartaoId == c.Id && contexto.Faturas.Any(f => f.Id == compra.FaturaId && f.PagaEm == null))
                .Sum(compra => (decimal?)compra.Valor) ?? 0m),
            Ct);
        Assert.Equal(0, divergentes);
    }

    private static async Task<List<HttpStatusCode>> StatusAsync(IEnumerable<Task<HttpResponseMessage>> requisicoes)
    {
        var respostas = await Task.WhenAll(requisicoes);
        var status = respostas.Select(r => r.StatusCode).ToList();
        foreach (var resposta in respostas)
        {
            resposta.Dispose();
        }

        return status;
    }
}
