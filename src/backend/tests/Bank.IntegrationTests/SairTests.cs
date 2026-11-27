using System.Net;
using System.Net.Http.Headers;
using Bank.Domain.Sessoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary><c>POST /api/v1/autenticacao/sair</c> (specs/016-sessao-refresh-token, US2; FR-011).</summary>
public class SairTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sair_204_RemoveCookie_ERevogaComLogout()
    {
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        using (var resposta = await ApiSessao.SairAsync(client, login.Cookie.Valor))
        {
            Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
            var remocao = CookieDeTeste.De(resposta)!;
            Assert.Equal(string.Empty, remocao.Valor);
            Assert.Equal("0", remocao.Atributo("max-age"));
        }

        using (var renovar = await ApiSessao.RenovarAsync(client, login.Cookie.Valor))
        {
            await ApiBank.AssertProblemaAsync(renovar, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        }

        await using var contexto = factory.CriarContexto();
        var hash = RenovacaoTests.Hash(login.Cookie.Valor);
        var sessaoId = (await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct)).SessaoId;
        Assert.Equal(MotivoRevogacao.Logout, (await contexto.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoId, Ct)).MotivoRevogacao);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("inventada")]
    public async Task Sair_SemCookieOuDesconhecido_204(string? cookie)
    {
        using var client = factory.CreateClient();

        using var resposta = await ApiSessao.SairAsync(client, cookie);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.NotNull(CookieDeTeste.De(resposta));
    }

    [Fact]
    public async Task Sair_Repetido_Idempotente()
    {
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        (await ApiSessao.SairAsync(client, login.Cookie.Valor)).Dispose();
        using var segunda = await ApiSessao.SairAsync(client, login.Cookie.Valor);

        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
    }

    [Fact]
    public async Task TokenDeAcessoEmitidoAntes_ContinuaValendoAteExpirar()
    {
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        (await ApiSessao.SairAsync(client, login.Cookie.Valor)).Dispose();

        using var requisicao = new HttpRequestMessage(HttpMethod.Get, ApiBank.MinhaCarteira);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        using var resposta = await client.SendAsync(requisicao, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Corrida_RenovarESairComACredencialAntiga_SessaoRevogada()
    {
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);
        var renovada = await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);

        (await ApiSessao.SairAsync(client, login.Cookie.Valor)).Dispose();

        using var resposta = await ApiSessao.RenovarAsync(client, renovada.Cookie.Valor);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
