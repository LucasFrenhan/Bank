using Bank.Infrastructure.Persistencia;

namespace Bank.ArchitectureTests;

/// <summary>
/// SQL puro só em arquivos <c>.sql</c> embutidos em <c>Persistencia/Sql</c> (constituição
/// v1.1.0; specs/006-transferencias, data-model).
/// </summary>
public class SqlEmbutidoTests
{
    public static TheoryData<string> Arquivos() =>
    [
        "Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql",
        "Procedures/RemoverImpedimentoDeAlteracao.sql",
        "Consultas/BloquearCarteirasParaTransferencia.sql",
        "Consultas/CriarLancamentosDeAbertura.sql",
    ];

    [Theory]
    [MemberData(nameof(Arquivos))]
    public void ArquivoSql_EstaEmbutidoEPodeSerLido(string caminho)
    {
        Assert.Contains(caminho, SqlEmbutido.Disponiveis());
        Assert.StartsWith("-- Origem:", SqlEmbutido.Ler(caminho), StringComparison.Ordinal);
    }

    [Fact]
    public void ArquivoInexistente_FalhaComMensagemClara()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => SqlEmbutido.Ler("Consultas/NaoExiste.sql"));
        Assert.Contains("Consultas/NaoExiste.sql", erro.Message, StringComparison.Ordinal);
    }
}
