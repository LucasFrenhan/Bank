namespace Bank.Worker;

/// <summary>
/// Marcador do assembly do Worker para o <c>WebApplicationFactory</c> dos testes, sem conflitar
/// com o <c>Program</c> da API (specs/010-mensageria-rabbitmq, research R12).
/// </summary>
public sealed class WorkerAssemblyMarker
{
    private WorkerAssemblyMarker()
    {
    }
}
