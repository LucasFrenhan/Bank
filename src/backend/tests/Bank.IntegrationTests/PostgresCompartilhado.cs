using Npgsql;
using Testcontainers.PostgreSql;

namespace Bank.IntegrationTests;

/// <summary>
/// Um único contêiner PostgreSQL por execução de testes, com a mesma imagem do Compose
/// (constituição, Princípio VII; research R10). Cada <see cref="BankApiFactory"/> recebe um
/// banco próprio dentro dele, o que isola as classes de teste que rodam em paralelo.
/// O contêiner é removido pelo Testcontainers (Ryuk) ao fim do processo.
/// </summary>
public static class PostgresCompartilhado
{
    public const string Imagem = "postgres:18.7-alpine";

    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(IniciarAsync);

    /// <summary>Connection string para um banco novo e exclusivo (criado pelas migrations).</summary>
    public static async Task<string> CriarConnectionStringAsync()
    {
        var container = await Container.Value;
        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"bank_testes_{Guid.NewGuid():N}",
        };

        return builder.ConnectionString;
    }

    private static async Task<PostgreSqlContainer> IniciarAsync()
    {
        var container = new PostgreSqlBuilder(Imagem).Build();
        await container.StartAsync();
        return container;
    }
}
