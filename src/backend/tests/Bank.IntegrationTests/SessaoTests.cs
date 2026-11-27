using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Bank.Domain.Sessoes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.IntegrationTests;

/// <summary>
/// Login com sessão no servidor, cookie e persistência (specs/016-sessao-refresh-token, fundação; FR-001, FR-003,
/// FR-004, FR-012, FR-012a, FR-022, FR-023).
/// </summary>
public class SessaoTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_EmiteCookieComOsAtributosDoContrato_ENoStore()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));

        using var resposta = await client.PostAsJsonAsync(ApiBank.Login, new { email, senha = Dados.SenhaValida }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(resposta.Headers.CacheControl?.NoStore);
        var cookie = CookieDeTeste.De(resposta);
        Assert.NotNull(cookie);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", cookie.Valor);
        Assert.True(cookie.Tem("httponly"));
        Assert.True(cookie.Tem("secure"));
        Assert.Equal("strict", cookie.Atributo("samesite"), ignoreCase: true);
        Assert.Equal("/api/v1/autenticacao", cookie.Atributo("path"));
        Assert.Equal("900", cookie.Atributo("max-age"));
        Assert.Null(cookie.Atributo("domain"));

        // O corpo nunca leva a credencial.
        var corpo = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(cookie.Valor, corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_GravaSoOHashDaCredencial()
    {
        using var client = factory.CreateClient();
        var (_, sessao) = await ApiSessao.CadastrarEEntrarAsync(client);
        var esperado = SHA256.HashData(Encoding.UTF8.GetBytes(sessao.Cookie.Valor));
        var valorEmBytes = Encoding.UTF8.GetBytes(sessao.Cookie.Valor);

        await using var contexto = factory.CriarContexto();
        var hash = HashCredencial.De(esperado);
        var credencial = await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct);

        Assert.True(credencial.Ativa);
        Assert.Equal(32, credencial.Hash.Bytes.Length);
        Assert.False(credencial.Hash.Bytes.AsSpan().SequenceEqual(valorEmBytes));
        var sessaoGravada = await contexto.Sessoes.AsNoTracking().SingleAsync(s => s.Id == credencial.SessaoId, Ct);
        Assert.Null(sessaoGravada.RevogadaEm);
    }

    [Fact]
    public async Task Login_FalhaDeCredenciais_SemCookie()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));

        using var resposta = await client.PostAsJsonAsync(ApiBank.Login, new { email, senha = "Errada@123" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Null(CookieDeTeste.De(resposta));
    }

    [Fact]
    public async Task NovoLogin_ComCookieDeSessaoAtiva_RevogaSoAAnterior()
    {
        using var client = factory.CreateClient();
        var (email, navegadorA) = await ApiSessao.CadastrarEEntrarAsync(client);
        var navegadorB = await ApiSessao.EntrarAsync(client, email);

        var novaA = await ApiSessao.EntrarAsync(client, email, navegadorA.Cookie.Valor);

        using (var antiga = await ApiSessao.RenovarAsync(client, navegadorA.Cookie.Valor))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, antiga.StatusCode);
        }

        await ApiSessao.RenovarComSucessoAsync(client, navegadorB.Cookie.Valor); // FR-012a: outra sessão intocada
        await ApiSessao.RenovarComSucessoAsync(client, novaA.Cookie.Valor);

        await using var contexto = factory.CriarContexto();
        var hash = HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(navegadorA.Cookie.Valor)));
        var sessaoId = (await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct)).SessaoId;
        var revogada = await contexto.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoId, Ct);
        Assert.Equal(MotivoRevogacao.NovoLogin, revogada.MotivoRevogacao);
    }

    [Fact]
    public async Task EndpointProtegido_ContinuaExigindoToken()
    {
        using var client = factory.CreateClient();
        var (_, sessao) = await ApiSessao.CadastrarEEntrarAsync(client);

        using (var semToken = await client.GetAsync(ApiBank.MinhaCarteira, Ct))
        {
            await ApiBank.AssertProblemaAsync(semToken, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        }

        // O cookie sozinho não autentica chamadas da API.
        using var soCookie = new HttpRequestMessage(HttpMethod.Get, ApiBank.MinhaCarteira);
        soCookie.Headers.Add("Cookie", $"{CookieDeTeste.Nome}={sessao.Cookie.Valor}");
        using var resposta = await client.SendAsync(soCookie, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Banco_RecusaHashForaDoTamanho_DuasAtivas_E_RevogacaoSemMotivo()
    {
        using var client = factory.CreateClient();
        var (_, sessao) = await ApiSessao.CadastrarEEntrarAsync(client);
        await using var contexto = factory.CriarContexto();
        var hash = HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(sessao.Cookie.Valor)));
        var sessaoId = (await contexto.CredenciaisRenovacao.AsNoTracking().SingleAsync(c => c.Hash == hash, Ct)).SessaoId.Valor;

        await AssertViolacaoAsync(contexto, PostgresErrorCodes.CheckViolation,
            "INSERT INTO credenciais_renovacao (id, sessao_id, hash, emitida_em, expira_em) VALUES (gen_random_uuid(), @s, decode(repeat('ab', 31), 'hex'), now(), now())", sessaoId);
        await AssertViolacaoAsync(contexto, PostgresErrorCodes.UniqueViolation,
            "INSERT INTO credenciais_renovacao (id, sessao_id, hash, emitida_em, expira_em) VALUES (gen_random_uuid(), @s, decode(repeat('cd', 32), 'hex'), now(), now())", sessaoId);
        await AssertViolacaoAsync(contexto, PostgresErrorCodes.CheckViolation,
            "UPDATE sessoes SET revogada_em = now() WHERE id = @s", sessaoId);
    }

    [Fact]
    public async Task Banco_ExcluirUsuario_ApagaSessoesECredenciais()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        await ApiSessao.EntrarAsync(client, email);
        var usuarioId = cadastro.GetProperty("usuarioId").GetGuid();
        var carteiraId = cadastro.GetProperty("carteiraId").GetGuid();
        await using var contexto = factory.CriarContexto();

        // Só para o teste: o domínio não exclui usuários; a carteira e os lançamentos de abertura saem antes.
        await contexto.Database.ExecuteSqlRawAsync(
            "ALTER TABLE lancamentos DISABLE TRIGGER USER; DELETE FROM lancamentos WHERE carteira_id = @c; ALTER TABLE lancamentos ENABLE TRIGGER USER;"
            + "DELETE FROM carteiras WHERE id = @c; DELETE FROM usuarios WHERE id = @u;",
            [new NpgsqlParameter("c", carteiraId), new NpgsqlParameter("u", usuarioId)],
            Ct);

        Assert.Equal(0, await contexto.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM sessoes WHERE usuario_id = @u", new NpgsqlParameter("u", usuarioId)).SingleAsync(Ct));
        Assert.Equal(0, await contexto.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM credenciais_renovacao c LEFT JOIN sessoes s ON s.id = c.sessao_id WHERE s.id IS NULL").SingleAsync(Ct));
    }

    private static async Task AssertViolacaoAsync(DbContext contexto, string sqlState, string sql, Guid sessaoId)
    {
        var erro = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlRawAsync(sql, [new NpgsqlParameter("s", sessaoId)], Ct));
        Assert.Equal(sqlState, erro.SqlState);
    }

    internal static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);
}
