namespace Bank.Worker.Modulos;

/// <summary>
/// Ponto único de registro dos módulos no Worker (constituição v1.3.0, Princípio VIII; specs/017-modulos-fundacao,
/// contracts/formato-modulo.md). Chamado depois do registro do núcleo antigo, sem mudar a ordem dele. Incluir um
/// módulo é uma linha:
/// <code>
/// servicos.AddModulo&lt;Nome&gt;(configuracao).AddModulo&lt;Nome&gt;Worker(configuracao);
/// </code>
/// O Worker só conhece a classe Modulo&lt;Nome&gt; e o contrato público de cada módulo (regra MOD-06).
/// </summary>
public static class RegistroModulos
{
    /// <summary>Serviços e processos em segundo plano de cada módulo. Nenhum módulo extraído ainda (#58).</summary>
    public static IServiceCollection AddModulos(this IServiceCollection servicos, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        return servicos;
    }
}
