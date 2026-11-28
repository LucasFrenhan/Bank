using Bank.Modulos.Alfa.Application;
using Bank.Modulos.Alfa.Contratos;
using Bank.Modulos.Alfa.Endpoints;
using Bank.Modulos.Alfa.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.Modulos.Alfa;

/// <summary>
/// Ponto de registro do módulo de exemplo (contracts/formato-modulo.md): o único tipo público fora dos contratos.
/// Os hosts chamam estes métodos a partir do RegistroModulos e não conhecem nenhum tipo interno de Alfa.
/// </summary>
public static class ModuloAlfa
{
    /// <summary>Serviços do módulo (API e Worker).</summary>
    public static IServiceCollection AddModuloAlfa(this IServiceCollection servicos, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        servicos.AddDbContext<AlfaDbContext>();
        servicos.AddScoped<IAlfaRepositorio, AlfaRepositorio>();
        servicos.AddScoped<IAlfaConsultas, ConsultarAlfa>();
        return servicos;
    }

    /// <summary>Endpoints do módulo (só API).</summary>
    public static IEndpointRouteBuilder MapModuloAlfa(this IEndpointRouteBuilder rotas) => rotas.MapAlfa();

    /// <summary>Processos em segundo plano do módulo (só Worker).</summary>
    public static IServiceCollection AddModuloAlfaWorker(this IServiceCollection servicos, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        servicos.AddHostedService<AlfaHospedado>();
        return servicos;
    }
}
