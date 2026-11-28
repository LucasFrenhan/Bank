using System.Text.RegularExpressions;

namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Convenção do ponto de registro (contracts/formato-modulo.md): fora do contrato público, o único tipo público de
/// um módulo é <c>Bank.Modulos.&lt;Nome&gt;.Modulo&lt;Nome&gt;</c>; todo o resto é <c>internal</c> (Princípio VIII).
/// </summary>
public partial class PontoDeRegistroTests
{
    [Fact]
    public void ModulosReais_SoExpoemOPontoDeRegistro()
    {
        var indevidos = CarregadorArquitetura.AssembliesDeModulos()
            .Where(assembly => !assembly.GetName().Name!.EndsWith(".Contratos", StringComparison.Ordinal))
            .SelectMany(assembly => TiposPublicosIndevidos(assembly.GetExportedTypes()))
            .ToList();

        Assert.True(indevidos.Count == 0, "Torne internal (só Modulo<Nome> e os contratos são públicos): " + string.Join(", ", indevidos));
    }

    [Fact]
    public void Exemplos_ApontamOTipoPublicoIndevidoEAceitamOPontoDeRegistro()
    {
        var exportados = typeof(Bank.Modulos.Alfa.Contratos.IAlfaConsultas).Assembly.GetExportedTypes();

        Assert.Equal(["Bank.Modulos.Gama.Application.TipoPublicoIndevido"], TiposPublicosIndevidos(exportados));
        Assert.Contains(exportados, t => t.FullName == "Bank.Modulos.Alfa.ModuloAlfa");
    }

    /// <summary>Tipos públicos de módulo fora de <c>.Contratos</c> que não são o ponto de registro.</summary>
    internal static List<string> TiposPublicosIndevidos(IEnumerable<Type> exportados) =>
        [
            .. exportados
                .Select(tipo => (Tipo: tipo, Modulo: ModuloRegex().Match(tipo.Namespace ?? string.Empty)))
                .Where(x => x.Modulo.Success && x.Modulo.Groups["parte"].Value != "Contratos")
                .Where(x => x.Tipo.FullName != $"Bank.Modulos.{x.Modulo.Groups["modulo"].Value}.Modulo{x.Modulo.Groups["modulo"].Value}")
                .Select(x => x.Tipo.FullName!)
                .Order(StringComparer.Ordinal),
        ];

    [GeneratedRegex(@"^Bank\.Modulos\.(?<modulo>[^.]+)(\.(?<parte>[^.]+))?")]
    private static partial Regex ModuloRegex();
}
