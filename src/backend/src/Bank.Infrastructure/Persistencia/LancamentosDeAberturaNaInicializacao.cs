using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Cria, ao iniciar, o lançamento de abertura das carteiras cujo saldo não está coberto por
/// lançamentos (carteiras anteriores ao razão) — specs/006-transferencias, FR-016; research R5.
/// Só roda com <see cref="PersistenciaOptions.CriarLancamentosDeAberturaNaInicializacao"/>
/// ligado (Development e Testing; nunca em Production). Registrado depois das migrations.
/// </summary>
internal sealed class LancamentosDeAberturaNaInicializacao(IServiceScopeFactory scopeFactory, IOptions<PersistenciaOptions> opcoes) : IHostedService
{
    public const string ArquivoSql = "Consultas/CriarLancamentosDeAbertura.sql";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!opcoes.Value.CriarLancamentosDeAberturaNaInicializacao)
        {
            return;
        }

        await using var escopo = scopeFactory.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<BankDbContext>();
        await contexto.Database.ExecuteSqlRawAsync(SqlEmbutido.Ler(ArquivoSql), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
