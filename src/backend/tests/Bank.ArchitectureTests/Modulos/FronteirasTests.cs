namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Regras de fronteira do Princípio VIII (constituição v1.3.0; specs/017-modulos-fundacao, US2 e US3) sobre a
/// arquitetura real: núcleo antigo, hosts, Shared Kernel e todo assembly Bank.Modulos.*. Violação nova, exceção
/// obsoleta e exceção incompleta quebram o build.
/// </summary>
public class FronteirasTests
{
    [Fact]
    public void ArquiteturaReal_SoTemAsViolacoesDaListaDeExcecoes()
    {
        var comparacao = ExcecoesConhecidas.Comparar(RegrasFronteira.Avaliar(CarregadorArquitetura.Real()), ExcecoesConhecidas.Todas);

        Assert.True(comparacao.Ok, comparacao.Descrever());
    }

    [Fact]
    public void ListaDeExcecoes_TemTodasAsEntradasCompletas()
    {
        var incompletas = ExcecoesConhecidas.Comparar([], ExcecoesConhecidas.Todas).Incompletas;

        Assert.True(incompletas.Count == 0, new ComparacaoExcecoes([], [], incompletas).Descrever());
    }

    [Fact]
    public void Carregador_EncontraOSharedKernel()
    {
        var namespaces = CarregadorArquitetura.Real().Types.Select(t => t.Namespace.FullName).ToHashSet();

        Assert.Contains("Bank.SharedKernel", namespaces);
        Assert.Contains("Bank.SharedKernel.Infrastructure.Persistencia", namespaces);
    }
}
