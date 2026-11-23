using System.Globalization;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bank.IntegrationTests;

/// <summary>GET /api/v1/carteiras/minha (US3; 401/403 exigidos pelo Princípio VII).</summary>
public class MinhaCarteiraTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UsuarioAutenticado_200ComCorpoDoContrato()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var token = await ApiBank.ObterTokenAsync(client, email);

        using var resposta = await GetComTokenAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\"saldo\":10.00", corpo, StringComparison.Ordinal);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(cadastro.GetProperty("carteiraId").GetString(), json.GetProperty("carteiraId").GetString());
        Assert.Equal(cadastro.GetProperty("usuarioId").GetString(), json.GetProperty("usuarioId").GetString());
        Assert.Equal("Lucas Silva", json.GetProperty("nomeTitular").GetString());
        Assert.Equal("PessoaFisica", json.GetProperty("tipoConta").GetString());
        Assert.Equal("BRL", json.GetProperty("moeda").GetString());
        Assert.Equal(TimeSpan.Zero, json.GetProperty("atualizadoEm").GetDateTimeOffset().Offset);
    }

    /// <summary>specs/012-dados-bancarios-carteira (contracts/carteira-minha.md 1.1.0; FR-009).</summary>
    [Fact]
    public async Task UsuarioAutenticado_ContaBancariaComOsDadosGravados()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var token = await ApiBank.ObterTokenAsync(client, email);
        await using var contexto = factory.CriarContexto();
        var carteira = await contexto.Carteiras.AsNoTracking()
            .SingleAsync(c => c.Id == CarteiraId.De(Guid.Parse(cadastro.GetProperty("carteiraId").GetString()!)), Ct);

        using var resposta = await GetComTokenAsync(client, token);

        var conta = (await ApiBank.LerJsonAsync(resposta)).GetProperty("contaBancaria");
        var banco = conta.GetProperty("banco");
        Assert.Equal(JsonValueKind.String, banco.GetProperty("codigo").ValueKind);
        Assert.Equal("001", banco.GetProperty("codigo").GetString());
        Assert.Equal("One Bank", banco.GetProperty("nome").GetString());
        Assert.Equal("0001", conta.GetProperty("agencia").GetString());
        Assert.Equal(JsonValueKind.String, conta.GetProperty("numero").ValueKind);
        Assert.Equal(carteira.NumeroConta, conta.GetProperty("numero").GetString());
        Assert.Equal(carteira.DigitoConta, conta.GetProperty("digito").GetString());
        Assert.Equal(ContaBancaria.CalcularDigito("0001", carteira.NumeroConta), conta.GetProperty("digito").GetString());
        Assert.Equal("ContaCorrente", conta.GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task PessoaJuridica_SaldoZero()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPj(email));

        using var resposta = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, email));

        Assert.Contains("\"saldo\":0.00", await resposta.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoisUsuarios_CadaTokenVeSoASuaCarteira()
    {
        using var client = factory.CreateClient();
        var (emailA, emailB) = (Dados.Email(), Dados.Email());
        var a = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(emailA));
        var b = await ApiBank.RegistrarAsync(client, Dados.CadastroPj(emailB));

        using var respostaA = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, emailA));
        using var respostaB = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, emailB));

        Assert.Equal(a.GetProperty("carteiraId").GetString(), (await ApiBank.LerJsonAsync(respostaA)).GetProperty("carteiraId").GetString());
        Assert.Equal(b.GetProperty("carteiraId").GetString(), (await ApiBank.LerJsonAsync(respostaB)).GetProperty("carteiraId").GetString());
    }

    [Fact]
    public async Task SaldoAlteradoNaBaseDepoisDoLogin_ConsultaMostraSaldoAtual()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var token = await ApiBank.ObterTokenAsync(client, email);
        var carteiraId = cadastro.GetProperty("carteiraId").GetGuid();

        await using (var contexto = factory.CriarContexto())
        {
            await contexto.Carteiras.Where(c => c.Id == CarteiraId.De(carteiraId)).ExecuteUpdateAsync(s => s.SetProperty(c => c.Saldo, 123.45m), Ct);
        }

        using var resposta = await GetComTokenAsync(client, token);

        Assert.Equal(123.45m, (await ApiBank.LerJsonAsync(resposta)).GetProperty("saldo").GetDecimal());
    }

    [Fact]
    public async Task SemToken_401ComWwwAuthenticate()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(ApiBank.MinhaCarteira, Ct);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        Assert.Contains(resposta.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    public static TheoryData<string> CenariosDeTokenInvalido() =>
        ["expirado", "adulterado", "outra-chave", "alg-none", "hs512", "outro-emissor", "outra-audiencia", "lixo"];

    [Theory]
    [MemberData(nameof(CenariosDeTokenInvalido))]
    public async Task TokenInvalido_401(string cenario)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        var chave = factory.ChaveJwt;
        var agora = DateTime.UtcNow;

        var token = cenario switch
        {
            "expirado" => TokensDeTeste.Criar(chave, claims, emitidoEm: agora.AddSeconds(-3601), expiraEm: agora.AddSeconds(-1)),
            "adulterado" => Adulterar(TokensDeTeste.Criar(chave, claims)),
            "outra-chave" => TokensDeTeste.Criar(new string('x', 64), claims),
            "alg-none" => TokensDeTeste.Criar(chave, claims, assinar: false),
            "hs512" => TokensDeTeste.Criar(chave, claims, algoritmo: SecurityAlgorithms.HmacSha512),
            "outro-emissor" => TokensDeTeste.Criar(chave, claims, emissor: "outro"),
            "outra-audiencia" => TokensDeTeste.Criar(chave, claims, audiencia: "outra"),
            _ => "nao-e-um-jwt",
        };

        using var resposta = await GetComTokenAsync(client, token);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }

    [Theory]
    [InlineData("carteira_id")]
    [InlineData("tipo_conta")]
    [InlineData("sub")]
    public async Task TokenValidoSemClaimExigida_403(string claimAusente)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        claims.Remove(claimAusente);

        using var resposta = await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims));

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }

    [Fact]
    public async Task TokenValidoDeUsuarioInexistente_404()
    {
        // Token válido (assinado com a chave da API) de um usuário que não existe na base. Apagar
        // dados não é mais possível: lançamentos são imutáveis (specs/006-transferencias, R2).
        using var client = factory.CreateClient();
        var token = TokensDeTeste.Criar(factory.ChaveJwt, TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString()));

        using var resposta = await GetComTokenAsync(client, token);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, "urn:bank:problema:carteira-nao-encontrada");
    }

    private static async Task<HttpResponseMessage> GetComTokenAsync(HttpClient client, string token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, ApiBank.MinhaCarteira);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(requisicao, Ct);
    }

    /// <summary>Troca o tipo de conta no payload mantendo a assinatura original.</summary>
    private static string Adulterar(string token)
    {
        var partes = token.Split('.');
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlEncoder.Decode(partes[1]))!;
        var alterado = payload.ToDictionary(p => p.Key, p => (object)p.Value);
        alterado["tipo_conta"] = "PessoaJuridica";
        alterado["exp"] = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        partes[1] = Base64UrlEncoder.Encode(JsonSerializer.Serialize(alterado));
        return string.Join('.', partes);
    }
}
