using Bank.Modulos.Alfa.Application;
using Bank.Modulos.Alfa.Contratos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bank.Modulos.Alfa.Infrastructure;

internal sealed class AlfaDbContext(DbContextOptions<AlfaDbContext> opcoes) : DbContext(opcoes);

internal sealed class AlfaRepositorio(AlfaDbContext contexto) : IAlfaRepositorio
{
    public Domain.Alfa? Obter(AlfaId id) => new(id, $"alfa-{contexto.ContextId.Lease}");
}

/// <summary>Processo em segundo plano de exemplo, registrado só no Worker.</summary>
internal sealed class AlfaHospedado : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
