using Bank.Application;
using Bank.Application.Transferencias;
using Bank.Infrastructure;
using Bank.Worker.Observability;
using Bank.Worker.Saude;

namespace Bank.Worker;

/// <summary>
/// Raiz de composição do Worker de mensageria (specs/010-mensageria-rabbitmq, research R1):
/// despachante do outbox, consumidores e processamento diário (specs/014-cartao-credito-cobrancas). Classe explícita (sem top-level statements) para não
/// gerar um segundo <c>Program</c> público, que conflitaria com o da API nos testes.
/// </summary>
public static class ProgramaWorker
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddTelemetriaWorker();

        builder.Services
            .AddNotificacoes()
            .AddProcessamento()
            .AddPersistencia(builder.Configuration)
            .AddMensageria()
            .AddProcessamentoDiario();

        var app = builder.Build();

        // Falha na partida se o fuso de Brasília não existir (specs/014-cartao-credito-cobrancas, R7).
        _ = app.Services.GetRequiredService<CalendarioBrasilia>();

        app.MapSaude();

        await app.RunAsync();
    }
}
