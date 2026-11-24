using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Solicitação e consulta do cartão (specs/014-cartao-credito-cobrancas, US1; FR-001 a FR-008; SC-001, SC-003).</summary>
public class CartoesTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("001", "Visa", "4")]
    [InlineData("002", "Mastercard", "5")]
    public async Task Solicitar_EmiteComABandeiraEscolhidaELimitePadrao(string codigo, string nome, string primeiroDigito)
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var resposta = await SolicitarAsync(client, pf, codigo);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(
            ["cartaoId", "bandeira", "nomeImpresso", "ultimosDigitos", "validade", "limiteTotal", "limiteDisponivel", "comprasALancar", "criadoEm"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal((codigo, nome), (json.GetProperty("bandeira").GetProperty("codigo").GetString(), json.GetProperty("bandeira").GetProperty("nome").GetString()));
        Assert.Equal("LUCAS SILVA", json.GetProperty("nomeImpresso").GetString());
        Assert.Equal(("5000.00", "5000.00", "0.00"), (json.GetProperty("limiteTotal").GetRawText(), json.GetProperty("limiteDisponivel").GetRawText(), json.GetProperty("comprasALancar").GetRawText()));

        var dados = await GetOkAsync(client, pf.Token, MeusDados);
        Assert.StartsWith(primeiroDigito, dados.GetProperty("numero").GetString(), StringComparison.Ordinal);
        Assert.True(Bank.Domain.Cartoes.NumeroCartao.Criar(dados.GetProperty("numero").GetString()).Sucesso);
        Assert.Matches("^[0-9]{3}$", dados.GetProperty("cvv").GetString());
        Assert.Equal(json.GetProperty("validade").GetString(), dados.GetProperty("validade").GetString());
        Assert.EndsWith(json.GetProperty("ultimosDigitos").GetString()!, dados.GetProperty("numero").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("001")]
    [InlineData("002")]
    public async Task Solicitar_DeNovo_409_ECartaoNaoMuda(string segundaBandeira)
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        using var resposta = await SolicitarAsync(client, cartao.Conta, segundaBandeira);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, Tipo("cartao-ja-existente"));
        Assert.Equal("001", (await GetOkAsync(client, cartao.Conta.Token, MeuCartao)).GetProperty("bandeira").GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Solicitar_50Simultaneas_UmCartao()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => SolicitarAsync(client, pf, i % 2 == 0 ? "001" : "002")));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(49, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var contexto = factory.CriarContexto();
        Assert.Equal(1, await contexto.Cartoes.CountAsync(c => c.UsuarioId == Bank.Domain.Usuarios.UsuarioId.De(pf.UsuarioId), Ct));
        foreach (var r in respostas)
        {
            r.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("003")]
    [InlineData("Visa")]
    public async Task Solicitar_BandeiraInvalida_400_SemCartao(string? bandeira)
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var resposta = await SolicitarAsync(client, pf, bandeira);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.True(json.GetProperty("errors").TryGetProperty("bandeira", out _));
        using var consulta = await GetComTokenAsync(client, pf.Token, MeuCartao);
        await ApiBank.AssertProblemaAsync(consulta, HttpStatusCode.NotFound, Tipo("cartao-nao-encontrado"));
    }

    [Fact]
    public async Task ExibirDados_SempreOsMesmos_SemCache()
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        using var resposta = await GetComTokenAsync(client, cartao.Conta.Token, MeusDados);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(resposta.Headers.CacheControl!.NoStore);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal((cartao.Numero, cartao.Validade, cartao.Cvv, "LUCAS SILVA"),
            (json.GetProperty("numero").GetString()!, json.GetProperty("validade").GetString()!, json.GetProperty("cvv").GetString()!, json.GetProperty("nomeImpresso").GetString()));
    }

    [Fact]
    public async Task ConsultaComum_NaoTrazNumeroNemCvv()
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        var corpo = (await GetOkAsync(client, cartao.Conta.Token, MeuCartao)).GetRawText();

        Assert.DoesNotContain(cartao.Numero, corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("cvv", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExibirDados_SemCartao_404()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var resposta = await GetComTokenAsync(client, pf.Token, MeusDados);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, Tipo("cartao-nao-encontrado"));
    }

    [Fact]
    public async Task MudarOLimiteConfigurado_NaoAlteraCartaoExistente()
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        await using var outra = factory.WithWebHostBuilder(b => b.UseSetting("Cartoes:LimitePadrao", "123.45"));
        Assert.Equal(123.45m, outra.Services.GetRequiredService<IOptions<Bank.Application.Cartoes.CartoesOptions>>().Value.LimitePadrao);
        using var outroClient = outra.CreateClient();

        Assert.Equal(5000m, await LimiteDisponivelAsync(outroClient, cartao));
        var novo = await CriarPfAsync(outroClient);
        Assert.Equal(123.45m, (await PostOkAsync(outroClient, novo.Token, Cartoes, new { bandeira = "001" }, status: HttpStatusCode.Created)).GetProperty("limiteTotal").GetDecimal());
    }
}
