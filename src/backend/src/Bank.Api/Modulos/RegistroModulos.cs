namespace Bank.Api.Modulos;

/// <summary>
/// Ponto único de registro dos módulos na API (constituição v1.3.0, Princípio VIII; specs/017-modulos-fundacao,
/// contracts/formato-modulo.md). Chamado no Program depois do registro do núcleo antigo, sem mudar a ordem dele.
/// Incluir um módulo é uma linha em cada método:
/// <code>
/// servicos.AddModulo&lt;Nome&gt;(configuracao);   // em AddModulos
/// rotas.MapModulo&lt;Nome&gt;();                  // em MapModulos
/// </code>
/// A API só conhece a classe Modulo&lt;Nome&gt; e o contrato público de cada módulo (regra MOD-06).
/// </summary>
public static class RegistroModulos
{
    /// <summary>Serviços de cada módulo. Nenhum módulo extraído ainda (#58).</summary>
    public static IServiceCollection AddModulos(this IServiceCollection servicos, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        return servicos;
    }

    /// <summary>Endpoints de cada módulo. Nenhum módulo extraído ainda (#58).</summary>
    public static IEndpointRouteBuilder MapModulos(this IEndpointRouteBuilder rotas) => rotas;
}
