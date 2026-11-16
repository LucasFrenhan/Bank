using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bank.IntegrationTests;

public class HealthEndpointTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    [Fact]
    public async Task Health_SemAutenticacao_Retorna200Healthy()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Health_ComVerificacaoFalhando_Retorna503Unhealthy()
    {
        using var fabricaComFalha = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddHealthChecks().AddCheck("falha-simulada", () => HealthCheckResult.Unhealthy())));
        using var client = fabricaComFalha.CreateClient();

        using var resposta = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("Unhealthy", await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
