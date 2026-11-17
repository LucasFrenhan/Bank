using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bank.IntegrationTests;

/// <summary>GET /api/v1/usuarios/me (specs/005-usuarios-me; 401/403 exigidos pelo Princípio VII).</summary>
public class UsuariosMeTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static readonly Uri Me = new("/api/v1/usuarios/me", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PessoaFisica_200ComOsDadosDoCadastroESemCache()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cpf = Dados.Cpf();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email.ToUpperInvariant(), Dados.CpfComMascara(cpf)));
        var token = await ApiBank.ObterTokenAsync(client, email);

        using var resposta = await GetComTokenAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(resposta.Headers.CacheControl?.NoStore, "a resposta deve ter Cache-Control: no-store");
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(cadastro.GetProperty("usuarioId").GetString(), json.GetProperty("usuarioId").GetString());
        Assert.Equal("Lucas Silva", json.GetProperty("nomeCompleto").GetString());
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.Equal(cpf, json.GetProperty("documento").GetString());
        Assert.Equal("PessoaFisica", json.GetProperty("tipoConta").GetString());
        Assert.Equal(TimeSpan.Zero, json.GetProperty("criadoEm").GetDateTimeOffset().Offset);
        Assert.False(json.TryGetProperty("senhaHash", out _));
    }

    [Fact]
    public async Task PessoaJuridica_DocumentoAlfanumericoSemMascaraEmMaiusculas()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cnpj = Dados.CnpjAlfanumerico();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPj(email, Dados.CnpjComMascara(cnpj).ToLowerInvariant()));

        using var resposta = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, email));

        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(cnpj, json.GetProperty("documento").GetString());
        Assert.Equal("PessoaJuridica", json.GetProperty("tipoConta").GetString());
    }

    [Fact]
    public async Task DoisUsuarios_CadaTokenRecebeSoOsProprios()
    {
        using var client = factory.CreateClient();
        var (emailA, emailB) = (Dados.Email(), Dados.Email());
        var a = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(emailA));
        var b = await ApiBank.RegistrarAsync(client, Dados.CadastroPj(emailB));

        using var respostaA = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, emailA));
        using var respostaB = await GetComTokenAsync(client, await ApiBank.ObterTokenAsync(client, emailB));

        Assert.Equal(a.GetProperty("usuarioId").GetString(), (await ApiBank.LerJsonAsync(respostaA)).GetProperty("usuarioId").GetString());
        Assert.Equal(b.GetProperty("usuarioId").GetString(), (await ApiBank.LerJsonAsync(respostaB)).GetProperty("usuarioId").GetString());
    }

    [Fact]
    public async Task DadoAlteradoNaBaseDepoisDoLogin_RespostaMostraOValorAtual()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var token = await ApiBank.ObterTokenAsync(client, email);
        var usuarioId = cadastro.GetProperty("usuarioId").GetGuid();

        await using (var contexto = factory.CriarContexto())
        {
            await contexto.Database.ExecuteSqlAsync($"UPDATE usuarios SET nome_completo = 'Nome Atualizado' WHERE id = {usuarioId}", Ct);
        }

        using var resposta = await GetComTokenAsync(client, token);

        Assert.Equal("Nome Atualizado", (await ApiBank.LerJsonAsync(resposta)).GetProperty("nomeCompleto").GetString());
    }

    public static TheoryData<string> CenariosDeTokenInvalido() =>
        ["sem-token", "expirado", "adulterado", "outra-chave", "alg-none", "hs512", "outro-emissor", "outra-audiencia"];

    [Theory]
    [MemberData(nameof(CenariosDeTokenInvalido))]
    public async Task TokenInvalido_401(string cenario)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        var chave = factory.ChaveJwt;
        var agora = DateTime.UtcNow;

        string? token = cenario switch
        {
            "sem-token" => null,
            "expirado" => TokensDeTeste.Criar(chave, claims, emitidoEm: agora.AddSeconds(-3601), expiraEm: agora.AddSeconds(-1)),
            "adulterado" => TokensDeTeste.Criar(chave, claims)[..^4] + "AAAA",
            "outra-chave" => TokensDeTeste.Criar(new string('x', 64), claims),
            "alg-none" => TokensDeTeste.Criar(chave, claims, assinar: false),
            "hs512" => TokensDeTeste.Criar(chave, claims, algoritmo: SecurityAlgorithms.HmacSha512),
            "outro-emissor" => TokensDeTeste.Criar(chave, claims, emissor: "outro"),
            _ => TokensDeTeste.Criar(chave, claims, audiencia: "outra"),
        };

        using var resposta = token is null ? await client.GetAsync(Me, Ct) : await GetComTokenAsync(client, token);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
        Assert.Contains(resposta.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("tipo_conta")]
    [InlineData("carteira_id")]
    public async Task TokenSemClaimExigida_403(string claimAusente)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        claims.Remove(claimAusente);

        using var resposta = await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims));

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }

    [Fact]
    public async Task TokenComSubQueNaoEIdentificador_403()
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims("nao-e-um-guid", Guid.CreateVersion7().ToString());

        using var resposta = await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims));

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }

    [Fact]
    public async Task TokenValidoDeUsuarioRemovido_404()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var token = await ApiBank.ObterTokenAsync(client, email);
        var usuarioId = cadastro.GetProperty("usuarioId").GetGuid();

        await using (var contexto = factory.CriarContexto())
        {
            await contexto.Carteiras.Where(c => c.UsuarioId == usuarioId).ExecuteDeleteAsync(Ct);
            await contexto.Usuarios.Where(u => u.Id == usuarioId).ExecuteDeleteAsync(Ct);
        }

        using var resposta = await GetComTokenAsync(client, token);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, "urn:bank:problema:usuario-nao-encontrado");
    }

    private static async Task<HttpResponseMessage> GetComTokenAsync(HttpClient client, string token)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, Me);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(requisicao, Ct);
    }
}
