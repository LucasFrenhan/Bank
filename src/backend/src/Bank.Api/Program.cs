using Bank.Api.Endpoints;
using Bank.Api.ErrorHandling;
using Bank.Api.Observability;
using Bank.Api.OpenApi;
using Bank.Api.Testing;
using Bank.Application;
using Bank.Infrastructure;

// Raiz de composição: único ponto da API que conhece a camada de infraestrutura.
var builder = WebApplication.CreateBuilder(args);

builder.AddTelemetria();

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddTratamentoDeErros()
    .AddDocumentacaoApi()
    .AddHealthEndpoints();

var app = builder.Build();

app.UseTratamentoDeErros();

app.MapDocumentacaoApi();
app.MapHealthEndpoints();
app.MapTestEndpoints();

await app.RunAsync();

/// <summary>Exposto para os testes de integração (<c>WebApplicationFactory</c>).</summary>
public partial class Program;
