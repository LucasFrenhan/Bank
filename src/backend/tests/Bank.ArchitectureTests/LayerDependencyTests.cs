using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Bank.Api.Endpoints;
using Bank.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Bank.ArchitectureTests;

/// <summary>
/// Direção de dependências entre as camadas (constituição, Princípio II;
/// specs/002-setup-api, R9; specs/003-usuarios-carteiras, R10). Uma regra por teste para
/// mensagens de falha claras.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies(
            typeof(DomainAssemblyMarker).Assembly,
            typeof(Bank.Application.DependencyInjection).Assembly,
            typeof(Bank.Infrastructure.DependencyInjection).Assembly,
            typeof(HealthEndpoints).Assembly,
            typeof(Bank.Worker.WorkerAssemblyMarker).Assembly)
        .Build();

    private const string AspNetCore = @"^Microsoft\.AspNetCore(\..*)?$";
    private const string EntityFrameworkCore = @"^Microsoft\.EntityFrameworkCore(\..*)?$";
    private const string IdentityModel = @"^Microsoft\.IdentityModel(\..*)?$";
    private const string Npgsql = @"^Npgsql(\..*)?$";
    private const string RabbitMq = @"^RabbitMQ\.Client(\..*)?$";
    private const string OpenTelemetry = @"^OpenTelemetry(\..*)?$";

    [Fact]
    public void Domain_NaoDependeDeOutrasCamadasNemDeFrameworks()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Domain(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Bank\.(Application|Infrastructure|Api)(\..*)?$|" + AspNetCore + "|" + EntityFrameworkCore);

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Application_NaoDependeDeInfrastructureApiNemEfCore()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Application(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Bank\.(Infrastructure|Api)(\..*)?$|" + EntityFrameworkCore);

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Infrastructure_NaoDependeDaApi()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Infrastructure(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Bank\.Api(\..*)?$");

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Endpoints_NaoAcessamInfraestruturaNemDbContext()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Api\.Endpoints(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                @"^Bank\.Infrastructure(\..*)?$|" + EntityFrameworkCore);

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Domain_NaoDependeDeSegurancaNemDoDriverDoBanco()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Domain(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                IdentityModel + "|" + Npgsql + "|" + @"^Microsoft\.Extensions\.Identity(\..*)?$");

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Application_NaoDependeDeAspNetCoreJwtNemDoDriverDoBanco()
    {
        // Hash de senha e emissão de token ficam atrás de IHasherSenha/IEmissorToken (specs/003, R3/R4).
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Application(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                AspNetCore + "|" + IdentityModel + "|" + Npgsql);

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Endpoints_NaoAcessamODriverDoBanco()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Api\.Endpoints(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(Npgsql);

        AssertSemViolacoes(regra);
    }

    /// <summary>Broker e telemetria ficam na Infrastructure e nas raízes de composição (spec 010, R8).</summary>
    [Fact]
    public void DomainEApplication_NaoDependemDoBrokerNemDoOpenTelemetry()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.(Domain|Application)(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(RabbitMq + "|" + OpenTelemetry);

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Camadas_NaoDependemDoWorker()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.(Domain|Application|Infrastructure|Api)(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Bank\.Worker(\..*)?$");

        AssertSemViolacoes(regra);
    }

    /// <summary>O Worker só compõe: banco e broker são acessados pela Infrastructure (spec 010, R1).</summary>
    [Fact]
    public void Worker_NaoAcessaBancoNemBrokerDiretamente()
    {
        var regra = Types().That().ResideInNamespaceMatching(@"^Bank\.Worker(\..*)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(
                EntityFrameworkCore + "|" + Npgsql + "|" + RabbitMq + "|" + @"^Bank\.Infrastructure\.Persistencia(\..*)?$");

        AssertSemViolacoes(regra);
    }

    [Fact]
    public void Regras_NaoSaoVazias_CadaCamadaPossuiTipos()
    {
        foreach (var camada in new[] { @"^Bank\.Domain$", @"^Bank\.Application$", @"^Bank\.Infrastructure$", @"^Bank\.Api\.Endpoints$", @"^Bank\.Worker$" })
        {
            Assert.NotEmpty(Types().That().ResideInNamespaceMatching(camada).GetObjects(Arquitetura));
        }
    }

    private static void AssertSemViolacoes(TypesShouldConjunction regra)
    {
        var violacoes = regra.Evaluate(Arquitetura)
            .Where(resultado => !resultado.Passed)
            .Select(resultado => resultado.Description)
            .ToList();

        Assert.True(violacoes.Count == 0, $"{regra.Description}{Environment.NewLine}{string.Join(Environment.NewLine, violacoes)}");
    }
}
