using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Bank.IntegrationTests;

public class OpenApiTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static readonly Uri DocumentoOpenApi = new("/openapi/v1.json", UriKind.Relative);
    private static readonly Uri Scalar = new("/scalar/v1", UriKind.Relative);

    [Fact]
    public async Task DocumentoOpenApi_ForaDeProducao_DescreveHealthESemEndpointsDeTeste()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(DocumentoOpenApi, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var caminhos = documento.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/health", caminhos);
        Assert.DoesNotContain(caminhos, c => c.StartsWith("/__test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DocumentoOpenApi_DescreveEndpointsDeUsuariosECarteirasComBearer()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(DocumentoOpenApi, TestContext.Current.CancellationToken);

        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var raiz = documento.RootElement;
        var caminhos = raiz.GetProperty("paths");

        var registrar = caminhos.GetProperty("/api/v1/autenticacao/registrar").GetProperty("post").GetProperty("responses");
        Assert.True(registrar.TryGetProperty("201", out _));
        Assert.True(registrar.TryGetProperty("400", out _));
        Assert.True(registrar.TryGetProperty("409", out _));

        var login = caminhos.GetProperty("/api/v1/autenticacao/login").GetProperty("post").GetProperty("responses");
        Assert.True(login.TryGetProperty("200", out _));
        Assert.True(login.TryGetProperty("401", out _));

        var minha = caminhos.GetProperty("/api/v1/carteiras/minha").GetProperty("get");
        foreach (var status in new[] { "200", "401", "403", "404" })
        {
            Assert.True(minha.GetProperty("responses").TryGetProperty(status, out _), status);
        }

        Assert.True(minha.GetProperty("security")[0].TryGetProperty("Bearer", out _));

        // specs/005-usuarios-me
        var me = caminhos.GetProperty("/api/v1/usuarios/me").GetProperty("get");
        foreach (var status in new[] { "200", "401", "403", "404" })
        {
            Assert.True(me.GetProperty("responses").TryGetProperty(status, out _), $"usuarios/me {status}");
        }

        Assert.True(me.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.False(caminhos.GetProperty("/api/v1/autenticacao/login").GetProperty("post").TryGetProperty("security", out _));

        var bearer = raiz.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task Scalar_ForaDeProducao_RetornaPaginaHtml()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(Scalar, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/html", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task Documentacao_EmProducao_NaoEstaDisponivel(string rota)
    {
        using var producao = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = producao.CreateClient();

        using var resposta = await client.GetAsync(new Uri(rota, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
