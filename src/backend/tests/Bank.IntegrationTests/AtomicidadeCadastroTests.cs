using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Bank.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

/// <summary>
/// Fábrica em que o INSERT em <c>carteiras</c> falha depois de o INSERT em <c>usuarios</c> já
/// ter sido executado (um comando por INSERT, via <c>MaxBatchSize(1)</c>).
/// </summary>
public sealed class FalhaNaCarteiraApiFactory : BankApiFactory
{
    public InterceptadorFalhaCarteira Interceptador { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.ConfigureDbContext<BankDbContext>(opcoes => opcoes
            .UseNpgsql(ConnectionString, npgsql => npgsql.MaxBatchSize(1))
            .AddInterceptors(Interceptador)));
    }
}

public sealed class InterceptadorFalhaCarteira : DbCommandInterceptor
{
    private int _insercoesDeUsuario;
    private volatile bool _ativo;

    public bool Ativo
    {
        get => _ativo;
        set => _ativo = value;
    }

    public int InsercoesDeUsuarioExecutadas => Volatile.Read(ref _insercoesDeUsuario);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Verificar(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Verificar(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Verificar(DbCommand command)
    {
        if (!_ativo)
        {
            return;
        }

        if (command.CommandText.Contains("INSERT INTO usuarios", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _insercoesDeUsuario);
        }

        if (command.CommandText.Contains("INSERT INTO carteiras", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Falha simulada ao inserir a carteira.");
        }
    }
}

/// <summary>SC-002 / FR-012: usuário e carteira são gravados atomicamente (RN-04).</summary>
public class AtomicidadeCadastroTests(FalhaNaCarteiraApiFactory factory) : IClassFixture<FalhaNaCarteiraApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FalhaAoInserirCarteira_NaoDeixaUsuarioSemCarteira()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        factory.Interceptador.Ativo = true;

        try
        {
            using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, Dados.CadastroPf(email), Ct);

            await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.InternalServerError);
        }
        finally
        {
            factory.Interceptador.Ativo = false;
        }

        Assert.True(factory.Interceptador.InsercoesDeUsuarioExecutadas > 0, "O INSERT do usuário deveria ter sido executado antes da falha.");
        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.Usuarios.AnyAsync(u => u.Email == email, Ct));
    }

    [Fact]
    public async Task SemFalha_TodoUsuarioTemExatamenteUmaCarteira()
    {
        using var client = factory.CreateClient();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf());
        await ApiBank.RegistrarAsync(client, Dados.CadastroPj());

        await using var contexto = factory.CriarContexto();
        var usuarios = await contexto.Usuarios.CountAsync(Ct);
        var carteiras = await contexto.Carteiras.CountAsync(Ct);
        var usuariosSemUmaCarteira = await contexto.Usuarios
            .CountAsync(u => contexto.Carteiras.Count(c => c.UsuarioId == u.Id) != 1, Ct);

        Assert.True(usuarios >= 2);
        Assert.Equal(usuarios, carteiras);
        Assert.Equal(0, usuariosSemUmaCarteira);
    }
}
