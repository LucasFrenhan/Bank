using Microsoft.Extensions.DependencyInjection;

namespace Bank.Application;

/// <summary>Registro dos serviços da camada de aplicação.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Serviços de aplicação serão registrados pelas specs de negócio.
        return services;
    }
}
