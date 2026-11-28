using Bank.Exemplos.Host;
using Bank.Modulos.Alfa.Contratos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Registro de módulos nos hosts (specs/017-modulos-fundacao, US4; contracts/formato-modulo.md): o módulo de
/// exemplo Alfa entra pelo RegistroModulos de exemplo, que só conhece ModuloAlfa e o contrato público.
/// </summary>
public class RegistroModuloTests
{
    private static readonly IConfiguration Configuracao = new ConfigurationBuilder().Build();

    [Fact]
    public void AddModulos_RegistraOsServicosDoModulo()
    {
        using var provedor = new ServiceCollection().AddModulos(Configuracao).BuildServiceProvider();

        Assert.NotNull(provedor.GetRequiredService<IAlfaConsultas>());
    }

    [Fact]
    public async Task MapModulos_ExpoeOsEndpointsDoModulo()
    {
        var construtor = WebApplication.CreateBuilder();
        construtor.Services.AddModulos(construtor.Configuration);
        await using var app = construtor.Build();

        app.MapModulos();

        var rotas = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fonte => fonte.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);
        Assert.Contains("/exemplos/alfa/{id:guid}", rotas);
    }

    [Fact]
    public void AddModulosWorker_RegistraOsProcessosEmSegundoPlano()
    {
        var servicos = new ServiceCollection().AddModulosWorker(Configuracao);

        Assert.Contains(servicos, s => s.ServiceType == typeof(IHostedService) && s.ImplementationType?.Name == "AlfaHospedado");
    }
}
