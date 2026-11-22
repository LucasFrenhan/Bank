using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Nomes e argumentos da topologia (specs/010-mensageria-rabbitmq, FR-006; contracts/topologia-e-mensagens.md).</summary>
public class TopologiaMensageriaTests
{
    [Theory]
    [InlineData("notificacoes.transferencias.fila", 1000, "notificacoes.transferencias.espera.1000")]
    [InlineData("notificacoes.transferencias.fila", 4000, "notificacoes.transferencias.espera.4000")]
    [InlineData("outra", 100, "outra.espera.100")]
    public void FilaEspera_TrocaOSufixo(string fila, int intervalo, string esperado)
    {
        Assert.Equal(esperado, TopologiaMensageria.FilaEspera(fila, intervalo));
    }

    [Fact]
    public void FilaPrincipal_TemDeadLetterParaADlx()
    {
        var argumentos = TopologiaMensageria.ArgumentosFilaPrincipal(TopologiaMensageria.Transferencias);

        Assert.Equal("banco.eventos.dlx", argumentos["x-dead-letter-exchange"]);
        Assert.Equal("transferencia.concluida.dlq", argumentos["x-dead-letter-routing-key"]);
    }

    [Fact]
    public void FilaEspera_DevolveAFilaPrincipalPelaExchangePadrao()
    {
        var argumentos = TopologiaMensageria.ArgumentosFilaEspera(TopologiaMensageria.Transferencias, 2000);

        Assert.Equal(2000, argumentos["x-message-ttl"]);
        Assert.Equal(string.Empty, argumentos["x-dead-letter-exchange"]);
        Assert.Equal("notificacoes.transferencias.fila", argumentos["x-dead-letter-routing-key"]);
    }

    [Fact]
    public void Filas_SaoAsDoContrato()
    {
        Assert.Equal(
            [("notificacoes.transferencias.fila", "transferencia.concluida", "notificacoes.transferencias.dlq", "transferencia.concluida.dlq", true),
             ("notificacoes.cobrancas.fila", "cobranca.paga", "notificacoes.cobrancas.dlq", "cobranca.paga.dlq", false)],
            TopologiaMensageria.Filas.Select(f => (f.Fila, f.RoutingKey, f.FilaDlq, f.RoutingKeyDlq, f.ComFilasEspera)));
        Assert.Equal(("banco.eventos.topic", "banco.eventos.dlx"), (TopologiaMensageria.ExchangeEventos, TopologiaMensageria.ExchangeDlx));
    }
}
