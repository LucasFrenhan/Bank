using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Bank.Domain.Sessoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>
/// <c>POST /api/v1/autenticacao/renovar</c> (specs/016-sessao-refresh-token, US1 e US4; FR-006, FR-009, FR-010,
/// FR-016a; contracts/autenticacao-sessao-api.md).
/// </summary>
public class RenovacaoTests(SessaoApiFactory factory) : IClassFixture<SessaoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CookieValido_200_CorpoNovo_CookieNovo_ENoStore()
    {
        factory.Relogio.Agora = null;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        using var resposta = await ApiSessao.RenovarAsync(client, login.Cookie.Valor);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(resposta.Headers.CacheControl?.NoStore);
        var corpo = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal("Bearer", corpo.GetProperty("tipoToken").GetString());
        Assert.Equal(300, corpo.GetProperty("expiraEmSegundos").GetInt32());
        Assert.InRange(corpo.GetProperty("sessaoExpiraPorInatividadeEmSegundos").GetInt32(), 899, 900);
        Assert.InRange(corpo.GetProperty("sessaoExpiraEmSegundos").GetInt32(), 28790, 28800);
        var cookie = CookieDeTeste.De(resposta)!;
        Assert.NotEqual(login.Cookie.Valor, cookie.Valor);
        Assert.True(cookie.Tem("httponly"));
        Assert.Equal("/api/v1/autenticacao", cookie.Atributo("path"));

        // A antiga deixou de ser a ativa; o token novo vale nas rotas protegidas.
        await using (var contexto = factory.CriarContexto())
        {
            var antiga = await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == Hash(login.Cookie.Valor), Ct);
            Assert.False(antiga.Ativa);
            Assert.True((await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == Hash(cookie.Valor), Ct)).Ativa);
        }

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, ApiBank.MinhaCarteira);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", corpo.GetProperty("tokenAcesso").GetString());
        using var carteira = await client.SendAsync(requisicao, Ct);
        Assert.Equal(HttpStatusCode.OK, carteira.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("inventada")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task SemCookieOuDesconhecido_401NaoAutenticado_ERemoveOCookie(string? cookie)
    {
        using var client = factory.CreateClient();

        using var resposta = await ApiSessao.RenovarAsync(client, cookie);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        var remocao = CookieDeTeste.De(resposta)!;
        Assert.Equal(string.Empty, remocao.Valor);
        Assert.Equal("0", remocao.Atributo("max-age"));
        Assert.Equal("/api/v1/autenticacao", remocao.Atributo("path"));
    }

    [Fact]
    public async Task NaoExigeAuthorization_ETokenInvalidoNoCabecalhoNaoAtrapalha()
    {
        factory.Relogio.Agora = null;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, ApiSessao.Renovar);
        requisicao.Headers.Add("Cookie", $"{CookieDeTeste.Nome}={login.Cookie.Valor}");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "vencido.ou.invalido");
        using var resposta = await client.SendAsync(requisicao, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    // ---------- US4: inatividade e limite absoluto ----------

    [Fact]
    public async Task SemRenovarPor15Minutos_401()
    {
        var inicio = new DateTimeOffset(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);
        factory.Relogio.Agora = inicio;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        factory.Relogio.Agora = inicio.AddMinutes(14);
        var renovada = await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);
        Assert.Equal("900", renovada.Cookie.Atributo("max-age"));

        factory.Relogio.Agora = inicio.AddMinutes(14 + 15);
        using var resposta = await ApiSessao.RenovarAsync(client, renovada.Cookie.Valor);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        factory.Relogio.Agora = null;
    }

    [Fact]
    public async Task UsoContinuo_RecusadoNoLimiteAbsolutoDe8Horas_EMaxAgeAcompanha()
    {
        var inicio = new DateTimeOffset(2030, 2, 10, 8, 0, 0, TimeSpan.Zero);
        factory.Relogio.Agora = inicio;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);
        var cookie = login.Cookie.Valor;

        SessaoObtida? ultima = null;
        for (var minutos = 10; minutos < 8 * 60; minutos += 10)
        {
            factory.Relogio.Agora = inicio.AddMinutes(minutos);
            ultima = await ApiSessao.RenovarComSucessoAsync(client, cookie);
            cookie = ultima.Cookie.Valor;
        }

        // Aos 7h50: faltam 10 min para o limite absoluto, menos que a inatividade.
        Assert.Equal("600", ultima!.Cookie.Atributo("max-age"));
        Assert.Equal(600, ultima.Corpo.GetProperty("sessaoExpiraEmSegundos").GetInt32());
        Assert.Equal(600, ultima.Corpo.GetProperty("sessaoExpiraPorInatividadeEmSegundos").GetInt32());

        factory.Relogio.Agora = inicio.AddHours(8);
        using var resposta = await ApiSessao.RenovarAsync(client, cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        factory.Relogio.Agora = null;
    }

    internal static HashCredencial Hash(string valor) => HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
