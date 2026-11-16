using Bank.Api.Endpoints;
using Bank.Application;
using Bank.Infrastructure;

// Raiz de composição: único ponto da API que conhece a camada de infraestrutura.
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddHealthEndpoints();

var app = builder.Build();

app.MapHealthEndpoints();

await app.RunAsync();

/// <summary>Exposto para os testes de integração (<c>WebApplicationFactory</c>).</summary>
public partial class Program;
