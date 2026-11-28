using Bank.Modulos.Alfa.Contratos;

namespace Bank.Modulos.Beta.Application;

/// <summary>Módulo conforme: usa de Alfa só o contrato público.</summary>
internal static class NomeDoAlfa
{
    public static string? De(IAlfaConsultas alfa, Guid id) => alfa.Obter(new AlfaId(id))?.Nome;
}
