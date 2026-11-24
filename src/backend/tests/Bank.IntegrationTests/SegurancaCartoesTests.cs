using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// 401, 403 e OpenAPI de todos os endpoints da specs/014-cartao-credito-cobrancas (constituição, Princípio VII;
/// contracts/cartoes-cobrancas-api.md).
/// </summary>
public class SegurancaCartoesTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private const string Id = "019a8f77-1111-7890-abcd-ef1234567890";

    /// <summary>Método, rota e perfil sem permissão (nulo se qualquer autenticado pode).</summary>
    public static TheoryData<string, string, string?> Endpoints() => new()
    {
        { "POST", Cartoes, "PessoaJuridica" },
        { "GET", MeuCartao, "PessoaJuridica" },
        { "GET", MeusDados, "PessoaJuridica" },
        { "POST", Cobrancas, "PessoaFisica" },
        { "GET", MinhasCobrancas, "PessoaFisica" },
        { "GET", $"{Cobrancas}/{Id}", null },
        { "POST", $"{Cobrancas}/{Id}/cancelar", "PessoaFisica" },
        { "POST", $"{Cobrancas}/{Id}/pagar", "PessoaJuridica" },
        { "GET", Faturas, "PessoaJuridica" },
        { "GET", $"{Faturas}/2026-12", "PessoaJuridica" },
        { "POST", $"{Faturas}/2026-12/pagar", "PessoaJuridica" },
        { "GET", Recebiveis, "PessoaFisica" },
        { "GET", $"{Recebiveis}/{Id}", "PessoaFisica" },
        { "POST", $"{Recebiveis}/{Id}/antecipar", "PessoaFisica" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SemTokenOuTokenInvalido_401(string metodo, string rota, string? perfil)
    {
        using var client = factory.CreateClient();
        var forjado = TokensDeTeste.Criar("outra-chave-qualquer-com-mais-de-32-bytes!!", TokensDeTeste.Claims(Id, Id, perfil ?? "PessoaFisica"));

        using var semToken = await EnviarAsync(client, metodo, rota, null);
        using var invalido = await EnviarAsync(client, metodo, rota, forjado);

        await ApiBank.AssertProblemaAsync(semToken, HttpStatusCode.Unauthorized, Tipo("nao-autenticado"));
        await ApiBank.AssertProblemaAsync(invalido, HttpStatusCode.Unauthorized, Tipo("nao-autenticado"));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task PerfilErradoOuClaimsAusentes_403(string metodo, string rota, string? perfil)
    {
        using var client = factory.CreateClient();
        var semClaims = TokensDeTeste.Criar(factory.ChaveJwt, new Dictionary<string, object> { ["sub"] = Id });
        var subInvalido = TokensDeTeste.Claims(Id, Id, perfil == "PessoaFisica" ? "PessoaJuridica" : "PessoaFisica");
        subInvalido["sub"] = "nao-e-guid";

        using var ausentes = await EnviarAsync(client, metodo, rota, semClaims);
        await ApiBank.AssertProblemaAsync(ausentes, HttpStatusCode.Forbidden, Tipo("acesso-negado"));
        using var titularInvalido = await EnviarAsync(client, metodo, rota, TokensDeTeste.Criar(factory.ChaveJwt, subInvalido));
        await ApiBank.AssertProblemaAsync(titularInvalido, HttpStatusCode.Forbidden, Tipo("acesso-negado"));

        if (perfil is not null)
        {
            var conta = perfil == "PessoaFisica" ? await CriarPfAsync(client) : await CriarPjAsync(client);
            using var perfilErrado = await EnviarAsync(client, metodo, rota, conta.Token);
            await ApiBank.AssertProblemaAsync(perfilErrado, HttpStatusCode.Forbidden, Tipo("acesso-negado"));
        }
    }

    [Fact]
    public async Task OpenApi_DocumentaAsRotasEOsErros()
    {
        using var client = factory.CreateClient();
        using var resposta = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var caminhos = (await ApiBank.LerJsonAsync(resposta)).GetProperty("paths");

        Verificar(Cartoes, "post", "201", "400", "401", "403", "409");
        Verificar(MeuCartao, "get", "200", "401", "403", "404");
        Verificar(MeusDados, "get", "200", "401", "403", "404");
        Verificar(Cobrancas, "post", "201", "400", "401", "403");
        Verificar(MinhasCobrancas, "get", "200", "400", "401", "403");
        Verificar(Cobrancas + "/{cobrancaId}", "get", "200", "400", "401", "403", "404");
        Verificar(Cobrancas + "/{cobrancaId}/cancelar", "post", "200", "400", "401", "403", "404", "409");
        VerificarChave(Verificar(Cobrancas + "/{cobrancaId}/pagar", "post", "200", "400", "401", "403", "404", "409", "422"));
        Verificar(Faturas, "get", "200", "400", "401", "403", "404");
        Verificar(Faturas + "/{competencia}", "get", "200", "400", "401", "403", "404");
        VerificarChave(Verificar(Faturas + "/{competencia}/pagar", "post", "200", "400", "401", "403", "404", "409", "422"));
        Verificar(Recebiveis, "get", "200", "400", "401", "403");
        Verificar(Recebiveis + "/{recebivelId}", "get", "200", "400", "401", "403", "404");
        VerificarChave(Verificar(Recebiveis + "/{recebivelId}/antecipar", "post", "200", "400", "401", "403", "404", "409"));

        JsonElement Verificar(string rota, string metodo, params string[] status)
        {
            var operacao = caminhos.GetProperty(rota).GetProperty(metodo);
            Assert.Equal(status.Order(StringComparer.Ordinal), operacao.GetProperty("responses").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            Assert.True(operacao.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            return operacao;
        }

        static void VerificarChave(JsonElement operacao)
        {
            var chave = Assert.Single(operacao.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
            Assert.True(chave.GetProperty("required").GetBoolean());
        }
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string metodo, string rota, string? token)
    {
        using var requisicao = new HttpRequestMessage(new HttpMethod(metodo), new Uri(rota, UriKind.Relative));
        if (metodo == "POST")
        {
            requisicao.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        if (token is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(requisicao, TestContext.Current.CancellationToken);
    }
}
