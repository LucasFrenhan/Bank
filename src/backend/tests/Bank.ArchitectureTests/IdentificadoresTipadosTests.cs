using System.Reflection;
using Bank.Application;
using Bank.Domain;
using Bank.Domain.Comum;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Bank.ArchitectureTests;

/// <summary>
/// Identificadores tipados (constituição v1.2.0, "Restrições Técnicas"; issue #43): entidades,
/// referências entre agregados e assinaturas da Application usam <see cref="IIdentificador{TSelf}"/>,
/// nunca <see cref="Guid"/> cru, para o compilador recusar um <c>UsuarioId</c> onde se espera um
/// <c>CarteiraId</c>. As fronteiras (HTTP, SQL puro, evento) seguem em <see cref="Guid"/> e não são
/// verificadas aqui.
/// </summary>
public class IdentificadoresTipadosTests
{
    private const BindingFlags Propriedades = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags MembrosPublicos = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;

    private static readonly Type[] Identificadores = typeof(DomainAssemblyMarker).Assembly.GetTypes()
        .Where(ImplementaIdentificador)
        .ToArray();

    [Fact]
    public void Domain_NaoUsaGuidCruComoIdentificador()
    {
        var violacoes = typeof(DomainAssemblyMarker).Assembly.GetTypes()
            .SelectMany(tipo => tipo.GetProperties(Propriedades).Select(p => (Tipo: tipo, Propriedade: p)))
            .Where(x => TerminaEmId(x.Propriedade.Name) && EhGuid(x.Propriedade.PropertyType))
            .Select(x => $"{x.Tipo.FullName}.{x.Propriedade.Name}")
            .ToList();

        Assert.True(violacoes.Count == 0, "Use um identificador tipado (IIdentificador<T>) em: " + string.Join(", ", violacoes));
    }

    [Fact]
    public void Application_AssinaturasPublicasNaoUsamGuidCruComoIdentificador()
    {
        var tiposPublicos = typeof(DependencyInjection).Assembly.GetExportedTypes();

        var propriedades = tiposPublicos
            .SelectMany(tipo => tipo.GetProperties(MembrosPublicos).Select(p => (Tipo: tipo, Nome: p.Name, p.PropertyType)))
            .Where(x => TerminaEmId(x.Nome) && EhGuid(x.PropertyType))
            .Select(x => $"{x.Tipo.FullName}.{x.Nome}");

        var parametros = tiposPublicos
            .SelectMany(tipo => tipo.GetMethods(MembrosPublicos).Cast<MethodBase>()
                .Concat(tipo.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
                .Where(m => !m.IsSpecialName || m is ConstructorInfo)
                .SelectMany(m => m.GetParameters().Select(p => (Tipo: tipo, Metodo: m.Name, Parametro: p))))
            .Where(x => TerminaEmId(x.Parametro.Name ?? string.Empty) && EhGuid(x.Parametro.ParameterType))
            .Select(x => $"{x.Tipo.FullName}.{x.Metodo}({x.Parametro.Name})");

        var violacoes = propriedades.Concat(parametros).Distinct().ToList();

        Assert.True(violacoes.Count == 0, "Use um identificador tipado (IIdentificador<T>) em: " + string.Join(", ", violacoes));
    }

    [Fact]
    public void Identificadores_SaoReadonlyRecordStructComNovoUuidV7()
    {
        Assert.NotEmpty(Identificadores);
        var agora = DateTimeOffset.UtcNow;

        var violacoes = new List<string>();
        foreach (var tipo in Identificadores)
        {
            if (!tipo.IsValueType || !tipo.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute"))
            {
                violacoes.Add($"{tipo.Name}: não é readonly struct");
            }

            // Todo record struct tem o PrintMembers gerado pelo compilador.
            if (tipo.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is null)
            {
                violacoes.Add($"{tipo.Name}: não é record struct");
            }

            var novo = tipo.GetMethod("Novo", BindingFlags.Public | BindingFlags.Static, [typeof(DateTimeOffset)]);
            if (novo is null || novo.ReturnType != tipo)
            {
                violacoes.Add($"{tipo.Name}: falta static {tipo.Name} Novo(DateTimeOffset)");
                continue;
            }

            var valor = (Guid)tipo.GetProperty("Valor")!.GetValue(novo.Invoke(null, [agora]))!;
            if (valor.Version != 7)
            {
                violacoes.Add($"{tipo.Name}: Novo gera UUID v{valor.Version}, esperado v7");
            }
        }

        Assert.True(violacoes.Count == 0, string.Join("; ", violacoes));
    }

    [Fact]
    public void Infrastructure_PersisteTodoIdentificadorComoUuid()
    {
        var opcoes = new DbContextOptionsBuilder<BankDbContext>()
            .UseNpgsql("Host=localhost") // só monta o modelo; não abre conexão
            .Options;
        using var contexto = new BankDbContext(opcoes);

        var propriedades = contexto.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => ImplementaIdentificador(Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType))
            .ToList();

        Assert.NotEmpty(propriedades);
        var violacoes = propriedades
            .Where(p => p.GetTypeMapping().Converter?.ProviderClrType != typeof(Guid))
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}")
            .ToList();

        Assert.True(violacoes.Count == 0, "Registre o IdentificadorConverter no BankDbContext para: " + string.Join(", ", violacoes));
    }

    private static bool TerminaEmId(string nome) => nome.EndsWith("Id", StringComparison.OrdinalIgnoreCase);

    private static bool EhGuid(Type tipo) => (Nullable.GetUnderlyingType(tipo) ?? tipo) == typeof(Guid);

    private static bool ImplementaIdentificador(Type tipo) => tipo.GetInterfaces()
        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdentificador<>));
}
