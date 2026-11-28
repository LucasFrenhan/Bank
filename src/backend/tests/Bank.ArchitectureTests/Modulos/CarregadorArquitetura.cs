using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Bank.Api.Endpoints;
using Bank.Domain;
using Assembly = System.Reflection.Assembly;

namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Arquiteturas avaliadas pelas regras de fronteira (specs/017-modulos-fundacao, research R6): a real
/// (núcleo antigo, hosts, Shared Kernel e todo módulo presente na saída dos testes, sem configuração por
/// módulo) e a dos módulos de exemplo, carregadas uma única vez.
/// </summary>
internal static class CarregadorArquitetura
{
    private static readonly Lazy<Architecture> ArquiteturaReal = new(CarregarReal);

    private static readonly Lazy<Architecture> ArquiteturaExemplos = new(() =>
        new ArchLoader().LoadAssemblies(typeof(Bank.Modulos.Alfa.Contratos.IAlfaConsultas).Assembly).Build());

    public static Architecture Real() => ArquiteturaReal.Value;

    public static Architecture Exemplos() => ArquiteturaExemplos.Value;

    /// <summary>Assemblies de módulo (implementação e contratos) que chegaram à saída pela API e pelo Worker.</summary>
    public static IReadOnlyList<Assembly> AssembliesDeModulos() => CarregarDaSaida("Bank.Modulos.*.dll");

    private static Architecture CarregarReal()
    {
        Assembly[] nucleoEHosts =
        [
            typeof(DomainAssemblyMarker).Assembly,
            typeof(Bank.Application.DependencyInjection).Assembly,
            typeof(Bank.Infrastructure.DependencyInjection).Assembly,
            typeof(HealthEndpoints).Assembly,
            typeof(Bank.Worker.WorkerAssemblyMarker).Assembly,
        ];

        var assemblies = nucleoEHosts
            .Concat(CarregarDaSaida("Bank.SharedKernel*.dll"))
            .Concat(AssembliesDeModulos())
            .Distinct()
            .ToArray();

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static Assembly[] CarregarDaSaida(string padrao) =>
        Directory.GetFiles(AppContext.BaseDirectory, padrao)
            .Select(caminho => Assembly.Load(AssemblyName.GetAssemblyName(caminho)))
            .ToArray();
}
