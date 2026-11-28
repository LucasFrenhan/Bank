namespace Bank.Modulos.Alfa.Contratos;

/// <summary>Identificador tipado do módulo de exemplo, exposto no contrato público.</summary>
public readonly record struct AlfaId(Guid Valor);

public sealed record AlfaResumo(AlfaId Id, string Nome);

/// <summary>Consulta pública de Alfa: o que outros módulos e os hosts podem usar.</summary>
public interface IAlfaConsultas
{
    AlfaResumo? Obter(AlfaId id);
}
