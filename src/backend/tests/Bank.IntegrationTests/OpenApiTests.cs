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
