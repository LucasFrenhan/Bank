using System.Net;
using Bank.Domain.Sessoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>
/// Tolerância e reuso (specs/016-sessao-refresh-token, US3; FR-007, FR-008, FR-010; SC-003, SC-004): várias abas
/// renovando ao mesmo tempo não derrubam a sessão; reapresentar uma credencial trocada depois da tolerância revoga
/// a família inteira, com a mesma resposta de qualquer recusa.
/// </summary>
public class RenovacaoConcorrenteTests(SessaoApiFactory factory) : IClassFixture<SessaoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CincoRenovacoesParalelas_TodasAceitas_UmaUnicaCredencialAtiva()
    {
        factory.Relogio.Agora = null;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ApiSessao.RenovarAsync(client, login.Cookie.Valor)));

        try
        {
            Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var cookies = respostas.Select(r => CookieDeTeste.De(r)!.Valor).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(5, cookies.Count);

            await using var contexto = factory.CriarContexto();
            var hash = RenovacaoTests.Hash(login.Cookie.Valor);
            var sessaoId = (await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct)).SessaoId;
            var credenciais = await contexto.CredenciaisRenovacao.AsNoTracking().Where(c => c.SessaoId == sessaoId).ToListAsync(Ct);
            Assert.Equal(6, credenciais.Count);
            Assert.Single(credenciais, c => c.RotacionadaEm == null);
            Assert.Null((await contexto.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoId, Ct)).RevogadaEm);
        }
        finally
        {
            foreach (var resposta in respostas)
            {
                resposta.Dispose();
            }
        }
    }

    [Fact]
    public async Task ReusoDepoisDaTolerancia_401IgualAQualquerRecusa_ERevogaAFamilia()
    {
        var inicio = new DateTimeOffset(2030, 3, 10, 12, 0, 0, TimeSpan.Zero);
        factory.Relogio.Agora = inicio;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        factory.Relogio.Agora = inicio.AddMinutes(1);
        var renovada = await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);

        factory.Relogio.Agora = inicio.AddMinutes(1).AddSeconds(31);
        using var reuso = await ApiSessao.RenovarAsync(client, login.Cookie.Valor);
        using var desconhecida = await ApiSessao.RenovarAsync(client, "credencial-que-nunca-existiu");

        var corpoReuso = await ApiBank.AssertProblemaAsync(reuso, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        var corpoDesconhecida = await ApiBank.AssertProblemaAsync(desconhecida, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        foreach (var campo in new[] { "type", "title", "status", "detail" })
        {
            Assert.Equal(corpoDesconhecida.GetProperty(campo).GetRawText(), corpoReuso.GetProperty(campo).GetRawText());
        }

        Assert.Equal(CookieDeTeste.De(desconhecida)!.Bruto, CookieDeTeste.De(reuso)!.Bruto);

        // A credencial mais nova também caiu.
        using var nova = await ApiSessao.RenovarAsync(client, renovada.Cookie.Valor);
        Assert.Equal(HttpStatusCode.Unauthorized, nova.StatusCode);

        await using var contexto = factory.CriarContexto();
        var hash = RenovacaoTests.Hash(login.Cookie.Valor);
        var sessaoId = (await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct)).SessaoId;
        Assert.Equal(MotivoRevogacao.Reuso, (await contexto.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoId, Ct)).MotivoRevogacao);
        factory.Relogio.Agora = null;
    }

    [Fact]
    public async Task ReapresentacaoDentroDaTolerancia_Aceita()
    {
        var inicio = new DateTimeOffset(2030, 4, 10, 12, 0, 0, TimeSpan.Zero);
        factory.Relogio.Agora = inicio;
        using var client = factory.CreateClient();
        var (_, login) = await ApiSessao.CadastrarEEntrarAsync(client);

        factory.Relogio.Agora = inicio.AddMinutes(1);
        await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);
        factory.Relogio.Agora = inicio.AddMinutes(1).AddSeconds(20);

        var denovo = await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);

        await ApiSessao.RenovarComSucessoAsync(client, denovo.Cookie.Valor);
        factory.Relogio.Agora = null;
    }
}
