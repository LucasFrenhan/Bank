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

    /// <summary>specs/006-transferencias (contracts/transferencias-api.md).</summary>
    [Fact]
    public async Task DocumentoOpenApi_DescreveTransferenciasDestinatarioEExtrato()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(DocumentoOpenApi, TestContext.Current.CancellationToken);

        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var caminhos = documento.RootElement.GetProperty("paths");

        var transferir = caminhos.GetProperty("/api/v1/transferencias").GetProperty("post");
        AssertRespostas(transferir, "201", "200", "400", "401", "403", "404", "409", "422");
        var chave = Assert.Single(transferir.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.Equal("header", chave.GetProperty("in").GetString());
        Assert.True(chave.GetProperty("required").GetBoolean());

        var destinatario = caminhos.GetProperty("/api/v1/transferencias/destinatarios/consultar").GetProperty("post");
        AssertRespostas(destinatario, "200", "400", "401", "403", "404", "422");

        var extrato = caminhos.GetProperty("/api/v1/transferencias/extrato").GetProperty("get");
        AssertRespostas(extrato, "200", "400", "401", "403");
        var parametros = extrato.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        Assert.Equal(["direcao", "pagina", "tamanhoPagina"], parametros.Order(StringComparer.Ordinal));

        // Spec 008: consulta por identificador e comprovante v1.1.0.
        var consultar = caminhos.GetProperty("/api/v1/transferencias/{transferenciaId}").GetProperty("get");
        Assert.Equal("ConsultarTransferencia", consultar.GetProperty("operationId").GetString());
        AssertRespostas(consultar, "200", "400", "401", "403", "404");

        var esquemas = documento.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(esquemas.GetProperty("PagadorResposta").GetProperty("properties").TryGetProperty("documento", out _));
        var chaveComprovante = esquemas.GetProperty("ComprovanteResposta").GetProperty("properties").GetProperty("chaveIdempotencia");
        Assert.Contains("null", chaveComprovante.GetRawText(), StringComparison.Ordinal);

        // Spec 009: consulta de limites (contrato v1.2.0) e o 422 limite-excedido do envio.
        var limites = caminhos.GetProperty("/api/v1/transferencias/limites").GetProperty("get");
        Assert.Equal("ConsultarLimites", limites.GetProperty("operationId").GetString());
        AssertRespostas(limites, "200", "401", "403");
        var propriedadesLimites = esquemas.GetProperty("LimitesResposta").GetProperty("properties");
        foreach (var campo in new[] { "porOperacao", "diario", "transferidoHoje", "disponivelHoje", "dia" })
        {
            Assert.True(propriedadesLimites.TryGetProperty(campo, out _), campo);
        }

        Assert.Contains("limite-excedido", transferir.GetProperty("description").GetString(), StringComparison.Ordinal);

        foreach (var operacao in new[] { transferir, destinatario, extrato, consultar, limites })
        {
            Assert.True(operacao.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        }

        static void AssertRespostas(JsonElement operacao, params string[] status)
        {
            foreach (var s in status)
            {
                Assert.True(operacao.GetProperty("responses").TryGetProperty(s, out _), s);
            }
        }
    }

    /// <summary>specs/012-dados-bancarios-carteira (contracts/carteira-minha.md 1.1.0; FR-009).</summary>
    [Fact]
    public async Task DocumentoOpenApi_ContaBancariaSoNaConsultaDaCarteira()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.GetAsync(DocumentoOpenApi, TestContext.Current.CancellationToken);

        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var esquemas = documento.RootElement.GetProperty("components").GetProperty("schemas");

        var minha = esquemas.GetProperty("MinhaCarteiraResposta");
        Assert.True(minha.GetProperty("properties").TryGetProperty("contaBancaria", out _));
        Assert.Contains("contaBancaria", minha.GetProperty("required").EnumerateArray().Select(c => c.GetString()));

        var conta = esquemas.GetProperty("ContaBancariaResposta").GetProperty("properties");
        foreach (var campo in new[] { "agencia", "numero", "digito", "tipo" })
        {
            Assert.Equal("string", conta.GetProperty(campo).GetProperty("type").GetString());
        }

        var banco = esquemas.GetProperty("BancoResposta").GetProperty("properties");
        Assert.Equal("string", banco.GetProperty("codigo").GetProperty("type").GetString());
        Assert.Equal("string", banco.GetProperty("nome").GetProperty("type").GetString());

        var outros = esquemas.EnumerateObject()
            .Where(e => e.Name != "MinhaCarteiraResposta" && e.Value.TryGetProperty("properties", out var p) && p.TryGetProperty("contaBancaria", out _))
            .Select(e => e.Name)
            .ToList();
        Assert.Empty(outros);
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
