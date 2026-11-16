using Microsoft.Extensions.DependencyInjection;

namespace Bank.Infrastructure;

/// <summary>Registro dos serviços de infraestrutura (persistência, mensageria).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // DbContext, RabbitMQ e demais adaptadores serão registrados pelas specs que os usarem.
        return services;
    }
}
