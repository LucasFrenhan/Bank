using System.Text;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;

namespace Bank.ArchitectureTests.Modulos;

/// <summary>Dependência proibida entre dois tipos (nomes completos), com a regra violada.</summary>
public sealed record Violacao(string Regra, string Origem, string Destino);

/// <summary>
/// Regras de fronteira do Princípio VIII (constituição v1.3.0; specs/017-modulos-fundacao, research R6 e
/// contracts/regras-fronteira.md). Percorre as dependências de cada tipo no modelo do ArchUnitNET e devolve
/// pares origem → destino, que são a chave da lista de exceções. Cada dependência recebe só a primeira regra
/// que casar, na ordem MOD-02, MOD-01, MOD-03, MOD-04, MOD-05, MOD-06, MOD-07.
/// </summary>
public static partial class RegrasFronteira
{
    public static readonly IReadOnlyDictionary<string, string> Descricoes = new Dictionary<string, string>
    {
        ["MOD-01"] = "Módulo usa algo de outro módulo fora do contrato público",
        ["MOD-02"] = "Módulo usa a persistência de outro módulo",
        ["MOD-03"] = "Módulo depende do núcleo antigo ou de um host",
        ["MOD-04"] = "Camada do módulo depende de camada ou framework proibido",
        ["MOD-05"] = "Shared Kernel depende de módulo, núcleo antigo, host ou framework",
        ["MOD-06"] = "Host ou núcleo antigo usa interno de módulo",
        ["MOD-07"] = "Domínio, aplicação, endpoint ou contrato usa a infraestrutura comum",
    };

    private const string Contratos = "Contratos";
    private const string DbContext = "Microsoft.EntityFrameworkCore.DbContext";

    public static IReadOnlyList<Violacao> Avaliar(Architecture arquitetura)
    {
        var violacoes = new HashSet<Violacao>();

        foreach (var tipo in arquitetura.Types)
        {
            var origem = Local.De(tipo);
            foreach (var dependencia in tipo.Dependencies)
            {
                var destino = Local.De(dependencia.Target);
                if (origem.Nome == destino.Nome)
                {
                    continue;
                }

                if (Classificar(origem, destino, dependencia.Target) is { } regra)
                {
                    violacoes.Add(new Violacao(regra, origem.Nome, destino.Nome));
                }
            }
        }

        return
        [
            .. violacoes
                .OrderBy(v => v.Regra, StringComparer.Ordinal)
                .ThenBy(v => v.Origem, StringComparer.Ordinal)
                .ThenBy(v => v.Destino, StringComparer.Ordinal),
        ];
    }

    /// <summary>Texto da falha: uma linha por violação, com regra, origem e destino (FR-015).</summary>
    public static string Descrever(IEnumerable<Violacao> violacoes)
    {
        var texto = new StringBuilder("Violações de fronteira (constituição, Princípio VIII):");
        foreach (var v in violacoes)
        {
            texto.AppendLine().Append(v.Regra).Append(' ').Append(Descricoes[v.Regra]).Append(": ")
                .Append(v.Origem).Append(" → ").Append(v.Destino);
        }

        return texto.ToString();
    }

    private static string? Classificar(Local origem, Local destino, IType alvo)
    {
        if (origem.Modulo is { } m)
        {
            if (destino.Modulo is { } n && n != m)
            {
                if (destino.Parte == "Infrastructure" || DerivaDeDbContext(alvo))
                {
                    return "MOD-02";
                }

                if (destino.Parte != Contratos)
                {
                    return "MOD-01";
                }
            }

            if (destino.NucleoAntigo || destino.Host)
            {
                return "MOD-03";
            }

            if (ViolaCamada(origem, destino))
            {
                return "MOD-04";
            }
        }

        if (origem.KernelPuro && (destino.Modulo is not null || destino.NucleoAntigo || destino.Host || destino.KernelInfra ||
                                  FrameworkDoKernel().IsMatch(destino.Namespace)))
        {
            return "MOD-05";
        }

        if (origem.KernelInfra && (destino.Modulo is not null || destino.NucleoAntigo || destino.Host))
        {
            return "MOD-05";
        }

        if ((origem.Host || origem.NucleoAntigo) && destino.Modulo is { } modulo && destino.Parte != Contratos &&
            destino.Nome != $"Bank.Modulos.{modulo}.Modulo{modulo}")
        {
            return "MOD-06";
        }

        var usaInfraComumIndevido = NucleoDomainOuApplication().IsMatch(origem.Namespace) ||
                                    origem.Parte is "Domain" or "Application" or "Endpoints" or Contratos;
        return usaInfraComumIndevido && destino.KernelInfra ? "MOD-07" : null;
    }

    /// <summary>Princípio II dentro do módulo (MOD-04).</summary>
    private static bool ViolaCamada(Local origem, Local destino)
    {
        var mesmoModulo = destino.Modulo == origem.Modulo;
        return origem.Parte switch
        {
            "Domain" => (mesmoModulo && destino.Parte is "Application" or "Infrastructure" or "Endpoints") ||
                        FrameworkDeNegocio().IsMatch(destino.Namespace),
            "Application" => (mesmoModulo && destino.Parte is "Infrastructure" or "Endpoints") ||
                             FrameworkDeNegocio().IsMatch(destino.Namespace),
            "Endpoints" => (mesmoModulo && destino.Parte == "Infrastructure") ||
                           FrameworkDePersistencia().IsMatch(destino.Namespace),
            Contratos => (mesmoModulo && destino.Parte != Contratos) || !PermitidoEmContrato().IsMatch(destino.Namespace),
            _ => false,
        };
    }

    private static bool DerivaDeDbContext(IType alvo) =>
        alvo is Class classe && classe.InheritedClasses.Any(c => c.FullName == DbContext);

    /// <summary>Onde um tipo está: módulo e parte (Domain, Application, …), núcleo antigo, host ou Shared Kernel.</summary>
    private sealed record Local(string Nome, string Namespace, string? Modulo, string? Parte)
    {
        public bool NucleoAntigo => NucleoAntigoRegex().IsMatch(Namespace);

        public bool Host => HostRegex().IsMatch(Namespace);

        public bool KernelInfra => KernelInfraRegex().IsMatch(Namespace);

        public bool KernelPuro => KernelRegex().IsMatch(Namespace) && !KernelInfra;

        public static Local De(IType tipo)
        {
            // Tipos aninhados e gerados pelo compilador contam como o tipo de topo que os declara.
            var nome = tipo.FullName.Split('+', '/')[0];
            var ns = tipo.Namespace.FullName;
            var modulo = ModuloRegex().Match(ns);
            return modulo.Success
                ? new Local(nome, ns, modulo.Groups["modulo"].Value, modulo.Groups["parte"].Value)
                : new Local(nome, ns, null, null);
        }
    }

    [GeneratedRegex(@"^Bank\.Modulos\.(?<modulo>[^.]+)(\.(?<parte>[^.]+))?")]
    private static partial Regex ModuloRegex();

    [GeneratedRegex(@"^Bank\.(Domain|Application|Infrastructure)(\.|$)")]
    private static partial Regex NucleoAntigoRegex();

    [GeneratedRegex(@"^Bank\.(Domain|Application)(\.|$)")]
    private static partial Regex NucleoDomainOuApplication();

    [GeneratedRegex(@"^Bank\.(Api|Worker)(\.|$)")]
    private static partial Regex HostRegex();

    [GeneratedRegex(@"^Bank\.SharedKernel(\.|$)")]
    private static partial Regex KernelRegex();

    [GeneratedRegex(@"^Bank\.SharedKernel\.Infrastructure(\.|$)")]
    private static partial Regex KernelInfraRegex();

    [GeneratedRegex(@"^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|RabbitMQ\.Client|OpenTelemetry|Npgsql)(\.|$)")]
    private static partial Regex FrameworkDeNegocio();

    [GeneratedRegex(@"^(Microsoft\.EntityFrameworkCore|Npgsql)(\.|$)")]
    private static partial Regex FrameworkDePersistencia();

    [GeneratedRegex(@"^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Microsoft\.Extensions|RabbitMQ\.Client|OpenTelemetry|Npgsql)(\.|$)")]
    private static partial Regex FrameworkDoKernel();

    /// <summary>Contrato público: BCL, código gerado pelo compilador e tipos do Bank (as demais regras cuidam deles).</summary>
    [GeneratedRegex(@"^($|System(\.|$)|Microsoft\.CodeAnalysis(\.|$)|Bank\.)")]
    private static partial Regex PermitidoEmContrato();
}
