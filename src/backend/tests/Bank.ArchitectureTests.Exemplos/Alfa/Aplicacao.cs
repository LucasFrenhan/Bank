using Bank.Modulos.Alfa.Contratos;

namespace Bank.Modulos.Alfa.Application;

internal interface IAlfaRepositorio
{
    Domain.Alfa? Obter(AlfaId id);
}

internal sealed class ConsultarAlfa(IAlfaRepositorio repositorio) : IAlfaConsultas
{
    public AlfaResumo? Obter(AlfaId id) =>
        repositorio.Obter(id) is { } alfa ? new AlfaResumo(alfa.Id, alfa.Nome) : null;
}
