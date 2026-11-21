namespace Bank.IntegrationTests;

/// <summary>
/// A topologia do contrato é declarada pelo Worker, durável e idempotente
/// (specs/010-mensageria-rabbitmq, FR-006–FR-008; contracts/topologia-e-mensagens.md).
/// </summary>
public class TopologiaTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Worker_DeclaraExchangesFilasEBindingsDuraveis()
    {
        ambiente.Worker.Iniciar();
        var broker = ambiente.Worker.Broker;
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            try
            {
                await broker.ObterFilaAsync("notificacoes.cobrancas.dlq", Ct);
                return true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }), "A topologia não foi declarada.");

        foreach (var exchange in new[] { "banco.eventos.topic", "banco.eventos.dlx" })
        {
            var dados = await broker.ObterExchangeAsync(exchange, Ct);
            Assert.Equal("topic", dados.GetProperty("type").GetString());
            Assert.True(dados.GetProperty("durable").GetBoolean());
        }

        var principal = await broker.ObterFilaAsync("notificacoes.transferencias.fila", Ct);
        Assert.True(principal.GetProperty("durable").GetBoolean());
        var argumentos = principal.GetProperty("arguments");
        Assert.Equal("banco.eventos.dlx", argumentos.GetProperty("x-dead-letter-exchange").GetString());
        Assert.Equal("transferencia.concluida.dlq", argumentos.GetProperty("x-dead-letter-routing-key").GetString());
        Assert.Contains(("banco.eventos.topic", "transferencia.concluida"), await broker.ObterBindingsAsync("notificacoes.transferencias.fila", Ct));

        // Testes usam 100/200/400 ms (appsettings.Testing.json do Worker).
        foreach (var intervalo in new[] { 100, 200, 400 })
        {
            var espera = await broker.ObterFilaAsync($"notificacoes.transferencias.espera.{intervalo}", Ct);
            Assert.True(espera.GetProperty("durable").GetBoolean());
            var argumentosEspera = espera.GetProperty("arguments");
            Assert.Equal(intervalo, argumentosEspera.GetProperty("x-message-ttl").GetInt32());
            Assert.Equal(string.Empty, argumentosEspera.GetProperty("x-dead-letter-exchange").GetString());
            Assert.Equal("notificacoes.transferencias.fila", argumentosEspera.GetProperty("x-dead-letter-routing-key").GetString());
        }

        Assert.Contains(("banco.eventos.dlx", "transferencia.concluida.dlq"), await broker.ObterBindingsAsync("notificacoes.transferencias.dlq", Ct));
        Assert.Contains(("banco.eventos.topic", "cobranca.paga"), await broker.ObterBindingsAsync("notificacoes.cobrancas.fila", Ct));
        Assert.Contains(("banco.eventos.dlx", "cobranca.paga.dlq"), await broker.ObterBindingsAsync("notificacoes.cobrancas.dlq", Ct));
        Assert.True((await broker.ObterFilaAsync("notificacoes.cobrancas.dlq", Ct)).GetProperty("durable").GetBoolean());
    }

    [Fact]
    public async Task SegundoWorker_DeclaraDeNovoSemConflito()
    {
        ambiente.Worker.Iniciar();
        await using var segundo = new BankWorkerFactory(ambiente.Api.ConnectionString, ambiente.Worker.Broker);
        segundo.Iniciar();

        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            segundo.TextosDosLogs().Any(t => t.Contains("Topologia de mensageria declarada", StringComparison.Ordinal))));
        Assert.DoesNotContain(segundo.TextosDosLogs(), t => t.Contains("Falha ao declarar a topologia", StringComparison.Ordinal));
    }
}
