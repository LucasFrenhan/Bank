using System.Security.Cryptography;
using Bank.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

/// <summary>
/// Hospeda a API em memória no ambiente <c>Testing</c> (habilita os endpoints de teste e
/// desliga a exportação OTLP — specs/002-setup-api, research R5 e R7), com PostgreSQL real em
/// contêiner e banco exclusivo por fábrica, migrations aplicadas na inicialização
/// (<c>appsettings.Testing.json</c>) e chave JWT aleatória (specs/003-usuarios-carteiras, R4, R10).
/// </summary>
public class BankApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _connectionString;

    /// <summary>Chave de assinatura JWT desta fábrica (para os testes forjarem tokens).</summary>
    public string ChaveJwt { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>Chave de derivação dos cartões desta fábrica (specs/014-cartao-credito-cobrancas, R1).</summary>
    public string ChaveCartoes { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("A fábrica ainda não foi inicializada.");

    public async ValueTask InitializeAsync()
    {
        _connectionString = await PostgresCompartilhado.CriarConnectionStringAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Bank", ConnectionString);
        builder.UseSetting("Jwt:ChaveAssinatura", ChaveJwt);
        builder.UseSetting("Cartoes:ChaveDerivacao", ChaveCartoes);
    }

    /// <summary>Abre um contexto direto no banco desta fábrica (asserções sobre a base).</summary>
    public BankDbContext CriarContexto()
    {
        _ = Server; // garante a API iniciada (e as migrations aplicadas)
        return new BankDbContext(new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(ConnectionString).Options);
    }

    /// <summary>Escopo de serviços da API (para obter serviços registrados nos testes).</summary>
    public AsyncServiceScope CriarEscopo() => Services.CreateAsyncScope();
}
