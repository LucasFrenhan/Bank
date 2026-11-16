using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bank.IntegrationTests;

/// <summary>Fábrica que captura em memória os spans exportados pela API.</summary>
public sealed class TelemetryApiFactory : BankApiFactory
{
    public SynchronizedCollection<Activity> SpansExportados { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(SpansExportados)));
    }
}

public class TelemetryTests(TelemetryApiFactory factory) : IClassFixture<TelemetryApiFactory>
{
    [Fact]
    public async Task Requisicao_ExportaSpanComCaminhoEStatus()
    {
        using var client = factory.CreateClient();

        using var _ = await client.GetAsync(new Uri("/nao-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        var span = await AguardarSpanAsync(a => Equals(a.GetTagItem("url.path"), "/nao-existe"));
        Assert.NotNull(span);
        Assert.Equal(404, span.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task RotasDeRuido_NaoExportamSpan()
    {
        using var client = factory.CreateClient();

        using var _ = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var __ = await client.GetAsync(new Uri("/nao-existe-2", UriKind.Relative), TestContext.Current.CancellationToken);

        // Espera o span da segunda requisição: a partir daí, o da primeira já teria sido exportado.
        Assert.NotNull(await AguardarSpanAsync(a => Equals(a.GetTagItem("url.path"), "/nao-existe-2")));
        Assert.DoesNotContain(factory.SpansExportados.Copiar(), a => Equals(a.GetTagItem("url.path"), "/health"));
    }

    [Fact]
    public void Recurso_IdentificaServicoComoBankApi()
    {
        _ = factory.Server; // garante o host iniciado
        var tracerProvider = factory.Services.GetRequiredService<TracerProvider>();

        var atributos = tracerProvider.GetResource().Attributes.ToDictionary(a => a.Key, a => a.Value);

        Assert.Equal("bank-api", atributos["service.name"]);
    }

    private async Task<Activity?> AguardarSpanAsync(Func<Activity, bool> criterio)
    {
        for (var tentativa = 0; tentativa < 50; tentativa++)
        {
            var span = factory.SpansExportados.Copiar().FirstOrDefault(criterio);
            if (span is not null)
            {
                return span;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return null;
    }
}
