using Bank.Application;
using Bank.Infrastructure;
using Bank.Worker.Observability;
using Bank.Worker.Saude;

namespace Bank.Worker;

/// <summary>
/// Raiz de composição do Worker de mensageria (specs/010-mensageria-rabbitmq, research R1):
/// despachante do outbox e consumidores. Classe explícita (sem top-level statements) para não
/// gerar um segundo <c>Program</c> público, que conflitaria com o da API nos testes.
/// </summary>
public static class ProgramaWorker
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddTelemetriaWorker();

        builder.Services
            .AddApplication(ambienteDeProducao: builder.Environment.IsProduction())
            .AddPersistencia(builder.Configuration)
            .AddMensageria();

        var app = builder.Build();

        app.MapSaude();

        await app.RunAsync();
    }
}
