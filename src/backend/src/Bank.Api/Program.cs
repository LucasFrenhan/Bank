using Bank.Api.Endpoints;
using Bank.Api.ErrorHandling;
using Bank.Api.Observability;
using Bank.Api.OpenApi;
using Bank.Api.Seguranca;
using Bank.Api.Testing;
using Bank.Application;
using Bank.Infrastructure;

// Raiz de composição: único ponto da API que conhece a camada de infraestrutura.
var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetria();

builder.Services
    .AddApplication(ambienteDeProducao: builder.Environment.IsProduction())
    .AddInfrastructure(builder.Configuration)
    .AddTratamentoDeErros()
    .AddAutenticacaoJwt()
    .AddDocumentacaoApi()
    .AddHealthEndpoints();

var app = builder.Build();

app.UseTratamentoDeErros();
app.UseAuthentication();
app.UseAuthorization();

app.MapDocumentacaoApi();
app.MapHealthEndpoints();
app.MapAutenticacaoEndpoints();
app.MapCarteirasEndpoints();
app.MapUsuariosEndpoints();
app.MapTestEndpoints();

await app.RunAsync();

/// <summary>Exposto para os testes de integração (<c>WebApplicationFactory</c>).</summary>
public partial class Program;
