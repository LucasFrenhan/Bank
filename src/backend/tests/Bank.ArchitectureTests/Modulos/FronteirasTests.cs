namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Regras de fronteira do Princípio VIII (constituição v1.3.0; specs/017-modulos-fundacao, US2) sobre a
/// arquitetura real: núcleo antigo, hosts, Shared Kernel e todo assembly Bank.Modulos.*.
/// </summary>
public class FronteirasTests
{
    [Fact]
    public void ArquiteturaReal_NaoTemViolacoesDeFronteira()
    {
        var violacoes = RegrasFronteira.Avaliar(CarregadorArquitetura.Real());

        Assert.True(violacoes.Count == 0, RegrasFronteira.Descrever(violacoes));
    }

    [Fact]
    public void Carregador_EncontraOSharedKernel()
    {
        var namespaces = CarregadorArquitetura.Real().Types.Select(t => t.Namespace.FullName).ToHashSet();

        Assert.Contains("Bank.SharedKernel", namespaces);
        Assert.Contains("Bank.SharedKernel.Infrastructure.Persistencia", namespaces);
    }
}
