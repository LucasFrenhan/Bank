using System.Collections.Concurrent;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Leitura dos arquivos <c>.sql</c> de <c>Persistencia/Sql</c>, embutidos no assembly
/// (constituição v1.1.0: SQL puro nunca como string no C#). Caminhos relativos a
/// <c>Persistencia/Sql</c>, por exemplo <c>Consultas/BloquearCarteirasParaTransferencia.sql</c>.
/// </summary>
public static class SqlEmbutido
{
    private const string Prefixo = "Bank.Infrastructure.Persistencia.Sql.";

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Ler(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        return Cache.GetOrAdd(caminho, Carregar);
    }

    /// <summary>Caminhos de todos os arquivos embutidos (para verificação nos testes).</summary>
    public static IReadOnlyList<string> Disponiveis() =>
        [.. typeof(SqlEmbutido).Assembly.GetManifestResourceNames()
            .Where(nome => nome.StartsWith(Prefixo, StringComparison.Ordinal) && nome.EndsWith(".sql", StringComparison.Ordinal))
            .Select(ParaCaminho)
            .Order(StringComparer.Ordinal)];

    private static string Carregar(string caminho)
    {
        var recurso = Prefixo + caminho.Replace('/', '.').Replace('\\', '.');
        using var fluxo = typeof(SqlEmbutido).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Arquivo SQL não encontrado entre os recursos embutidos: {caminho}");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }

    private static string ParaCaminho(string recurso)
    {
        var relativo = recurso[Prefixo.Length..^".sql".Length];
        var ponto = relativo.IndexOf('.', StringComparison.Ordinal);
        return ponto < 0 ? relativo + ".sql" : $"{relativo[..ponto]}/{relativo[(ponto + 1)..]}.sql";
    }
}
