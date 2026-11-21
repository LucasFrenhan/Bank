using Bank.Infrastructure.Mensageria;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bank.Worker.Observability;

/// <summary>
/// Traces, métricas e logs do Worker via OpenTelemetry/OTLP (constituição, Princípio VI;
/// specs/010-mensageria-rabbitmq, FR-024, research R10). O endpoint vem de
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>; nos testes, os exportadores em memória são acrescentados.
/// </summary>
public static class TelemetriaWorker
{
    public const string NomeServico = "bank-worker";

    /// <summary>Meter das notificações (Application).</summary>
    public const string MeterNotificacoes = Bank.Application.Observabilidade.MetricasNotificacoes.NomeMeter;

    public static WebApplicationBuilder AddTelemetriaWorker(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(NomeServico))
            .WithTracing(tracing => tracing.AddSource(TelemetriaMensageria.Nome))
            .WithMetrics(metricas => metricas
                .AddMeter("System.Runtime")
                .AddMeter(TelemetriaMensageria.Nome)
                .AddMeter(MeterNotificacoes))
            .WithLogging();

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            openTelemetry.UseOtlpExporter();
        }

        return builder;
    }
}
