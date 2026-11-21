using Bank.Infrastructure.Mensageria;

namespace Bank.Worker.Saude;

/// <summary>
/// Saúde do Worker para o healthcheck do Compose (specs/010-mensageria-rabbitmq, FR-024):
/// <c>/health</c> = processo vivo; <c>/health/pronto</c> = conectado ao broker e com todos os
/// consumidores inscritos (503 caso contrário).
/// </summary>
public static class SaudeEndpoints
{
    public static WebApplication MapSaude(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

        app.MapGet("/health/pronto", (EstadoMensageria estado) =>
        {
            var corpo = new { status = estado.Pronto ? "Healthy" : "Unhealthy", consumidores = estado.Consumidores };
            return estado.Pronto ? Results.Ok(corpo) : Results.Json(corpo, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
