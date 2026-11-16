using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bank.Api.Endpoints;

/// <summary>
/// Endpoint público de saúde da aplicação (specs/002-setup-api, FR-002).
/// Implementado como route handler (e não com <c>MapHealthChecks</c>) para aparecer no
/// documento OpenAPI; mantém a semântica padrão: Healthy/Degraded → 200, Unhealthy → 503.
/// </summary>
public static class HealthEndpoints
{
    public const string Rota = "/health";

    // VIOLAÇÃO PROPOSITAL (SC-005) — branch descartável, nunca mesclar
    public static readonly Type Violacao = typeof(Bank.Infrastructure.DependencyInjection);

    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Rota, ConsultarSaudeAsync)
            .WithName("ConsultarSaude")
            .WithSummary("Indica se a API está em execução.")
            .WithTags("Saúde")
            .Produces<string>(StatusCodes.Status200OK, "text/plain")
            .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

        return app;
    }

    private static async Task<IResult> ConsultarSaudeAsync(HealthCheckService healthCheckService, CancellationToken cancellationToken)
    {
        var relatorio = await healthCheckService.CheckHealthAsync(cancellationToken);
        var status = relatorio.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return Results.Text(relatorio.Status.ToString(), "text/plain", statusCode: status);
    }
}
