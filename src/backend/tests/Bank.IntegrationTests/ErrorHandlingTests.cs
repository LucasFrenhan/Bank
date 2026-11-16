using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bank.IntegrationTests;

public class ErrorHandlingTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    [Fact]
    public async Task RotaInexistente_Retorna404ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(new Uri("/nao-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        await AssertProblemDetailsAsync(resposta, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MetodoNaoSuportado_Retorna405ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.PostAsync(new Uri("/health", UriKind.Relative), null, TestContext.Current.CancellationToken);

        await AssertProblemDetailsAsync(resposta, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task JsonMalformado_Retorna400ProblemDetails()
    {
        using var client = factory.CreateClient();
        using var corpo = new StringContent("{ \"texto\": ", Encoding.UTF8, "application/json");

        using var resposta = await client.PostAsync(new Uri("/__test/eco", UriKind.Relative), corpo, TestContext.Current.CancellationToken);

        await AssertProblemDetailsAsync(resposta, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JsonValido_NoEndpointDeEco_Retorna200()
    {
        using var client = factory.CreateClient();
        using var corpo = new StringContent("{ \"texto\": \"ola\" }", Encoding.UTF8, "application/json");

        using var resposta = await client.PostAsync(new Uri("/__test/eco", UriKind.Relative), corpo, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task FalhaNaoTratada_Retorna500SemDetalhesInternos()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(new Uri("/__test/falha", UriKind.Relative), TestContext.Current.CancellationToken);

        var corpo = await AssertProblemDetailsAsync(resposta, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("segredo-interno", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", corpo, StringComparison.Ordinal);
    }

    private static async Task<string> AssertProblemDetailsAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, resposta.StatusCode);
        Assert.Equal(new MediaTypeHeaderValue("application/problem+json").MediaType, resposta.Content.Headers.ContentType?.MediaType);

        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(corpo);
        Assert.Equal((int)esperado, json.RootElement.GetProperty("status").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("traceId").GetString());
        return corpo;
    }
}
