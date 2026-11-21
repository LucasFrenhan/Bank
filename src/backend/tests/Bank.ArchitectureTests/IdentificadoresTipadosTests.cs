using System.Reflection;
using Bank.Domain;

namespace Bank.ArchitectureTests;

/// <summary>
/// Identificadores do Domain são tipados (issue #43): nenhuma propriedade terminada em <c>Id</c>
/// pode ser <see cref="Guid"/> cru, para o compilador recusar um <c>UsuarioId</c> onde se espera
/// um <c>CarteiraId</c>.
/// </summary>
public class IdentificadoresTipadosTests
{
    private const BindingFlags Propriedades = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void Domain_NaoUsaGuidCruComoIdentificador()
    {
        var violacoes = typeof(DomainAssemblyMarker).Assembly.GetTypes()
            .SelectMany(tipo => tipo.GetProperties(Propriedades).Select(p => (Tipo: tipo, Propriedade: p)))
            .Where(x => x.Propriedade.Name.EndsWith("Id", StringComparison.Ordinal)
                && (Nullable.GetUnderlyingType(x.Propriedade.PropertyType) ?? x.Propriedade.PropertyType) == typeof(Guid))
            .Select(x => $"{x.Tipo.FullName}.{x.Propriedade.Name}")
            .ToList();

        Assert.True(violacoes.Count == 0, "Use um identificador tipado (IIdentificador<T>) em: " + string.Join(", ", violacoes));
    }
}
