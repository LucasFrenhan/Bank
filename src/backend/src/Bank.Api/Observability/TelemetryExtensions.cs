using Bank.Application.Observabilidade;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bank.Api.Observability;

/// <summary>
/// Traces, métricas e logs via OpenTelemetry/OTLP (constituição, Princípio VI;
/// specs/002-setup-api, R5). O endpoint vem de <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.
/// </summary>
public static class TelemetryExtensions
{
    public const string NomeServico = "bank-api";

    /// <summary>Rotas sem valor diagnóstico que não geram spans (métricas continuam).</summary>
    private static readonly string[] PrefixosIgnorados = ["/health", "/openapi", "/scalar"];

    public static WebApplicationBuilder AddTelemetria(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(NomeServico))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(opcoes => opcoes.Filter = contexto => !EhRotaIgnorada(contexto.Request.Path))
                .AddHttpClientInstrumentation())
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime")
                .AddMeter(MetricasUsuarios.NomeMeter))
            .WithLogging();

        // Nos testes não há coletor; eles acrescentam um exportador em memória (R5).
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            openTelemetry.UseOtlpExporter();
        }

        return builder;
    }

    private static bool EhRotaIgnorada(PathString caminho) =>
        PrefixosIgnorados.Any(prefixo => caminho.StartsWithSegments(prefixo, StringComparison.OrdinalIgnoreCase));
}
