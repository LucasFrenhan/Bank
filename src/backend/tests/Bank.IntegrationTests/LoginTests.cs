using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bank.IntegrationTests;

/// <summary>POST /api/v1/autenticacao/login (US2; contracts/usuarios-carteiras-api.md).</summary>
public class LoginTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CredenciaisCorretas_200ComTokenEClaimsDoCadastro()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPj(email));

        using var resposta = await client.PostAsJsonAsync(ApiBank.Login, new { email = email.ToUpperInvariant(), senha = Dados.SenhaValida }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal("Bearer", json.GetProperty("tipoToken").GetString());
        Assert.Equal(300, json.GetProperty("expiraEmSegundos").GetInt32());
        Assert.Equal(900, json.GetProperty("sessaoExpiraPorInatividadeEmSegundos").GetInt32());
        Assert.Equal(28800, json.GetProperty("sessaoExpiraEmSegundos").GetInt32());

        var token = new JsonWebToken(json.GetProperty("tokenAcesso").GetString());
        Assert.Equal("HS256", token.Alg);
        Assert.Equal(cadastro.GetProperty("usuarioId").GetString(), token.Subject);
        Assert.Equal(email, token.GetClaim("email").Value);
        Assert.Equal("PessoaJuridica", token.GetClaim("tipo_conta").Value);
        Assert.Equal(cadastro.GetProperty("carteiraId").GetString(), token.GetClaim("carteira_id").Value);
        Assert.Equal(TimeSpan.FromSeconds(300), token.ValidTo - token.IssuedAt);
        Assert.True(Guid.TryParse(token.GetClaim("sid").Value, out _));
    }

    [Fact]
    public async Task SenhaErradaEEmailInexistente_401Identicos()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));

        using var senhaErrada = await client.PostAsJsonAsync(ApiBank.Login, new { email, senha = "Errada@123" }, Ct);
        using var emailInexistente = await client.PostAsJsonAsync(ApiBank.Login, new { email = Dados.Email(), senha = Dados.SenhaValida }, Ct);

        var a = await ApiBank.AssertProblemaAsync(senhaErrada, HttpStatusCode.Unauthorized, "urn:bank:problema:credenciais-invalidas");
        var b = await ApiBank.AssertProblemaAsync(emailInexistente, HttpStatusCode.Unauthorized, "urn:bank:problema:credenciais-invalidas");
        foreach (var campo in new[] { "type", "title", "status", "detail" })
        {
            Assert.Equal(a.GetProperty(campo).GetRawText(), b.GetProperty(campo).GetRawText());
        }
    }

    [Theory]
    [InlineData(null, "SenhaForte@123", "email")]
    [InlineData("lucas@exemplo.com.br", null, "senha")]
    [InlineData("", "", "email")]
    public async Task CampoAusente_400ComErros(string? email, string? senha, string campo)
    {
        using var client = factory.CreateClient();

        using var resposta = await client.PostAsJsonAsync(ApiBank.Login, new { email, senha }, Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty(campo, out _));
    }
}
