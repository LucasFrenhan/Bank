using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

/// <summary>
/// Cookie de renovação nos testes (specs/016-sessao-refresh-token, research R4): o <c>CookieContainer</c> do .NET não
/// reenvia cookies <c>Secure</c> sobre HTTP, então os testes leem o <c>Set-Cookie</c> e mandam o cabeçalho
/// <c>Cookie</c> explicitamente — o que também permite reapresentar credenciais antigas (reuso).
/// </summary>
public sealed record CookieDeTeste(string Valor, IReadOnlyList<string> Atributos, string Bruto)
{
    public const string Nome = "bank_renovacao";

    public bool Tem(string atributo) => Atributos.Contains(atributo, StringComparer.OrdinalIgnoreCase);

    public string? Atributo(string nome) =>
        Atributos.Select(a => a.Split('=', 2)).Where(p => p.Length == 2 && p[0].Equals(nome, StringComparison.OrdinalIgnoreCase))
            .Select(p => p[1]).SingleOrDefault();

    public static CookieDeTeste? De(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        if (!resposta.Headers.TryGetValues("Set-Cookie", out var valores))
        {
            return null;
        }

        var bruto = valores.SingleOrDefault(v => v.StartsWith(Nome + "=", StringComparison.Ordinal));
        if (bruto is null)
        {
            return null;
        }

        var partes = bruto.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new CookieDeTeste(partes[0][(Nome.Length + 1)..], partes[1..], bruto);
    }
}

/// <summary>Resposta do login ou da renovação: corpo e cookie.</summary>
public sealed record SessaoObtida(JsonElement Corpo, CookieDeTeste Cookie)
{
    public string Token => Corpo.GetProperty("tokenAcesso").GetString()!;
}

public static class ApiSessao
{
    public static readonly Uri Renovar = new("/api/v1/autenticacao/renovar", UriKind.Relative);
    public static readonly Uri Sair = new("/api/v1/autenticacao/sair", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Cadastra uma PF e entra; devolve e-mail e a sessão.</summary>
    public static async Task<(string Email, SessaoObtida Sessao)> CadastrarEEntrarAsync(HttpClient client)
    {
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        return (email, await EntrarAsync(client, email));
    }

    public static async Task<SessaoObtida> EntrarAsync(HttpClient client, string email, string? cookie = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, ApiBank.Login)
        {
            Content = JsonContent.Create(new { email, senha = Dados.SenhaValida }),
        };
        ComCookie(requisicao, cookie);
        using var resposta = await client.SendAsync(requisicao, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return new SessaoObtida(await ApiBank.LerJsonAsync(resposta), CookieDeTeste.De(resposta)!);
    }

    public static Task<HttpResponseMessage> RenovarAsync(HttpClient client, string? cookie) => PostAsync(client, Renovar, cookie);

    public static Task<HttpResponseMessage> SairAsync(HttpClient client, string? cookie) => PostAsync(client, Sair, cookie);

    /// <summary>Renova e exige 200; devolve a sessão nova.</summary>
    public static async Task<SessaoObtida> RenovarComSucessoAsync(HttpClient client, string cookie)
    {
        using var resposta = await RenovarAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return new SessaoObtida(await ApiBank.LerJsonAsync(resposta), CookieDeTeste.De(resposta)!);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Uri rota, string? cookie)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota);
        ComCookie(requisicao, cookie);
        return await client.SendAsync(requisicao, Ct);
    }

    private static void ComCookie(HttpRequestMessage requisicao, string? cookie)
    {
        if (cookie is not null)
        {
            requisicao.Headers.Add("Cookie", $"{CookieDeTeste.Nome}={cookie}");
        }
    }
}

/// <summary>API com relógio ajustável (inatividade, limite absoluto e tolerância) e a limpeza registrada.</summary>
public class SessaoApiFactory : BankApiFactory
{
    public RelogioAjustavel Relogio { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sessao:InatividadeSegundos"] = "900",
            ["Sessao:LimiteAbsolutoHoras"] = "8",
            ["Sessao:ToleranciaReusoSegundos"] = "30",
        }));
        builder.ConfigureTestServices(servicos =>
        {
            servicos.AddSingleton<TimeProvider>(Relogio);
            Bank.Application.DependencyInjection.AddLimpezaSessoesAplicacao(servicos);
        });
    }
}
