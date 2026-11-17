using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Aplica as migrations pendentes na inicialização quando
/// <see cref="PersistenciaOptions.AplicarMigracoesNaInicializacao"/> está ligado (research R7).
/// Roda antes de a API aceitar requisições.
/// </summary>
internal sealed class MigracoesNaInicializacao(IServiceScopeFactory scopeFactory, IOptions<PersistenciaOptions> opcoes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!opcoes.Value.AplicarMigracoesNaInicializacao)
        {
            return;
        }

        await using var escopo = scopeFactory.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<BankDbContext>();
        await contexto.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
