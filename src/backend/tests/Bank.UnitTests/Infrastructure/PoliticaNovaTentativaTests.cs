using System.Text;
using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Contagem de execuções, filas de espera e DLQ (specs/010-mensageria-rabbitmq, FR-018,
/// FR-018a–c; contracts/topologia-e-mensagens.md).
/// </summary>
public class PoliticaNovaTentativaTests
{
    private static readonly int[] Intervalos = [1000, 2000, 4000];
    private static readonly DateTimeOffset Agora = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static DestinoFalha Falha(IDictionary<string, object?>? headers, Exception? erro = null, string? erroLeitura = null) =>
        PoliticaNovaTentativa.Decidir(TopologiaMensageria.Transferencias, Intervalos, headers, erro ?? (erroLeitura is null ? new TimeoutException("banco fora") : null), erroLeitura, Agora);

    [Fact]
    public void Contar_SemHeaders_PrimeiraExecucao()
    {
        Assert.Equal((1, 0), PoliticaNovaTentativa.Contar(null));
    }

    [Fact]
    public void Contar_ComTentativas_ProximaExecucao()
    {
        Assert.Equal((3, 0), PoliticaNovaTentativa.Contar(new Dictionary<string, object?> { ["x-tentativas"] = 2 }));
    }

    [Fact]
    public void Contar_VindaDaDlq_RecomecaEIncrementaReprocessamentos()
    {
        var daDlq = new Dictionary<string, object?> { ["x-tentativas"] = 4, ["x-falhou-em"] = Encoding.UTF8.GetBytes("2026-11-21T12:00:00Z") };
        Assert.Equal((1, 1), PoliticaNovaTentativa.Contar(daDlq));

        daDlq["x-reprocessamentos"] = 2;
        Assert.Equal((1, 3), PoliticaNovaTentativa.Contar(daDlq));
    }

    [Theory]
    [InlineData(0, 1, 1000)]
    [InlineData(1, 2, 2000)]
    [InlineData(2, 3, 4000)]
    public void FalhaAteATerceira_VaiParaAEsperaDoIntervalo(int tentativasAnteriores, int execucao, int intervalo)
    {
        var headers = new Dictionary<string, object?> { ["versao"] = 1, ["traceparent"] = "00-x", ["x-reprocessamentos"] = 1 };
        if (tentativasAnteriores > 0)
        {
            headers["x-tentativas"] = tentativasAnteriores;
        }

        var destino = Falha(headers);

        Assert.False(destino.ParaDlq);
        Assert.Equal((string.Empty, $"notificacoes.transferencias.espera.{intervalo}", execucao, intervalo), (destino.Exchange, destino.RoutingKey, destino.Execucao, destino.IntervaloMs));
        Assert.Equal(execucao, destino.Headers["x-tentativas"]);
        Assert.Equal(1, destino.Headers["x-reprocessamentos"]);
        Assert.Equal((1, "00-x"), (destino.Headers["versao"], destino.Headers["traceparent"]));
        Assert.DoesNotContain("x-motivo", destino.Headers.Keys);
        Assert.DoesNotContain("x-ultimo-erro", destino.Headers.Keys);
        Assert.DoesNotContain("x-falhou-em", destino.Headers.Keys);
    }

    [Fact]
    public void FalhaNaQuarta_VaiParaADlqComDiagnostico()
    {
        var destino = Falha(new Dictionary<string, object?> { ["x-tentativas"] = 3, ["traceparent"] = "00-x" });

        Assert.True(destino.ParaDlq);
        Assert.Equal(("banco.eventos.dlx", "transferencia.concluida.dlq"), (destino.Exchange, destino.RoutingKey));
        Assert.Equal(4, destino.Headers["x-tentativas"]);
        Assert.Equal("tentativas-esgotadas", destino.Headers["x-motivo"]);
        Assert.Equal("TimeoutException: banco fora", destino.Headers["x-ultimo-erro"]);
        Assert.Equal("2026-11-21T12:00:00.0000000Z", destino.Headers["x-falhou-em"]);
        Assert.Equal("00-x", destino.Headers["traceparent"]);
        Assert.DoesNotContain("x-reprocessamentos", destino.Headers.Keys);
    }

    [Fact]
    public void Invalida_VaiDiretoParaADlq()
    {
        var destino = Falha(null, erroLeitura: "Corpo inválido");

        Assert.True(destino.ParaDlq);
        Assert.Equal(1, destino.Headers["x-tentativas"]);
        Assert.Equal("mensagem-invalida", destino.Headers["x-motivo"]);
        Assert.Equal("MensagemInvalida: Corpo inválido", destino.Headers["x-ultimo-erro"]);
    }

    [Fact]
    public void Reprocessada_QueFalha_VaiParaAEsperaSemOsHeadersDaDlq()
    {
        var daDlq = new Dictionary<string, object?>
        {
            ["x-tentativas"] = 4,
            ["x-motivo"] = "tentativas-esgotadas",
            ["x-ultimo-erro"] = "Erro: antigo",
            ["x-falhou-em"] = "2026-11-21T11:00:00Z",
            ["x-death"] = new List<object?>(),
        };

        var destino = Falha(daDlq);

        Assert.Equal("notificacoes.transferencias.espera.1000", destino.RoutingKey);
        Assert.Equal((1, 1), (destino.Headers["x-tentativas"], destino.Headers["x-reprocessamentos"]));
        Assert.DoesNotContain("x-falhou-em", destino.Headers.Keys);
        Assert.DoesNotContain("x-motivo", destino.Headers.Keys);
        Assert.DoesNotContain("x-ultimo-erro", destino.Headers.Keys);
        Assert.DoesNotContain("x-death", destino.Headers.Keys);

        // Na volta da espera, conta como a segunda execução do reprocessamento, não como outro.
        Assert.Equal((2, 1), PoliticaNovaTentativa.Contar(destino.Headers));
    }

    [Fact]
    public void UltimoErro_TruncadoEm500Caracteres()
    {
        var destino = Falha(new Dictionary<string, object?> { ["x-tentativas"] = 3 }, new InvalidOperationException(new string('x', 1000)));

        Assert.Equal(500, ((string)destino.Headers["x-ultimo-erro"]!).Length);
    }
}
