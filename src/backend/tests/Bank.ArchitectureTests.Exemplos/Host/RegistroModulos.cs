using Bank.Modulos.Alfa;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.Exemplos.Host;

/// <summary>
/// Ponto único de registro de um host de exemplo (contracts/formato-modulo.md): o mesmo formato de
/// Bank.Api/Modulos/RegistroModulos.cs e Bank.Worker/Modulos/RegistroModulos.cs, com o módulo Alfa incluído.
/// </summary>
public static class RegistroModulos
{
    /// <summary>Como na API e no Worker: serviços de cada módulo.</summary>
    public static IServiceCollection AddModulos(this IServiceCollection servicos, IConfiguration configuracao) =>
        servicos.AddModuloAlfa(configuracao);

    /// <summary>Como na API: endpoints de cada módulo.</summary>
    public static IEndpointRouteBuilder MapModulos(this IEndpointRouteBuilder rotas) => rotas.MapModuloAlfa();

    /// <summary>Como no Worker: serviços e processos em segundo plano de cada módulo.</summary>
    public static IServiceCollection AddModulosWorker(this IServiceCollection servicos, IConfiguration configuracao) =>
        servicos.AddModulos(configuracao).AddModuloAlfaWorker(configuracao);
}
