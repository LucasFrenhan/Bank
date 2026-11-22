using Bank.Infrastructure.Mensageria;
using Bank.UnitTests.Application;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Laço do lote do despachante sem broker (specs/010-mensageria-rabbitmq, FR-002, research R4):
/// marca só os confirmados, para no primeiro que falha e conta a falha pelo motivo.
/// </summary>
public sealed class DespachanteOutboxTests : IDisposable
{
    private readonly MetricasCapturadasBase _metricas = new(f => new TelemetriaMensageria(f));

    public void Dispose() => _metricas.Dispose();

    [Theory]
    [InlineData("Nack", "nack")]
    [InlineData("Timeout", "timeout")]
    [InlineData("SemRota", "sem-rota")]
    [InlineData("CanalFechado", "canal")]
    public async Task Lote_ParaNoPrimeiroQueFalha(string desfecho, string motivo)
    {
        var falha = Enum.Parse<ResultadoPublicacao>(desfecho);
        var eventos = Enumerable.Range(0, 4).Select(_ => EventoOutbox.De(ContratoEventosTests.Evento())).ToList();
        var publicador = new PublicadorRoteirizado(ResultadoPublicacao.Confirmado, falha);

        var resultado = await Despachante().PublicarLoteAsync(eventos, publicador, TestContext.Current.CancellationToken);

        Assert.Equal(new ResultadoLote(4, 1, falha), resultado);
        Assert.NotNull(eventos[0].PublicadoEm);
        Assert.All(eventos.Skip(1), e => Assert.Null(e.PublicadoEm));
        Assert.Equal(2, publicador.Chamadas.Count); // os seguintes não são tentados
        Assert.Equal([eventos[0].Id.Valor.ToString(), eventos[1].Id.Valor.ToString()], publicador.Chamadas.Select(c => c.MessageId));
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "banco.eventos.publicacao.falhas" && m.Tags["motivo"] == motivo);
        Assert.Single(_metricas.Medicoes, m => m.Nome == "banco.eventos.publicados");
    }

    [Fact]
    public async Task Lote_TodosConfirmados_MarcaTodosEmOrdem()
    {
        var eventos = Enumerable.Range(0, 3).Select(_ => EventoOutbox.De(ContratoEventosTests.Evento())).ToList();
        var publicador = new PublicadorRoteirizado();

        var resultado = await Despachante().PublicarLoteAsync(eventos, publicador, TestContext.Current.CancellationToken);

        Assert.Equal(new ResultadoLote(3, 3, null), resultado);
        Assert.All(eventos, e => Assert.NotNull(e.PublicadoEm));
        Assert.All(publicador.Chamadas, c => Assert.Equal(("banco.eventos.topic", "transferencia.concluida"), (c.Exchange, c.RoutingKey)));
    }

    [Fact]
    public void Espera_CresceComAsFalhasAteODezSegundos_ESegueNaHoraComLoteCheio()
    {
        var opcoes = new MensageriaOptions { IntervaloDespachoMs = 1000, TamanhoLoteDespacho = 50 };
        var falha = new ResultadoLote(1, 0, ResultadoPublicacao.Nack);

        Assert.Equal(TimeSpan.FromSeconds(2), DespachanteOutbox.EsperaAteAProximaRodada(falha, 1, opcoes));
        Assert.Equal(TimeSpan.FromSeconds(8), DespachanteOutbox.EsperaAteAProximaRodada(falha, 3, opcoes));
        Assert.Equal(TimeSpan.FromSeconds(10), DespachanteOutbox.EsperaAteAProximaRodada(falha, 6, opcoes));
        Assert.Equal(TimeSpan.Zero, DespachanteOutbox.EsperaAteAProximaRodada(new ResultadoLote(50, 50, null), 0, opcoes));
        Assert.Equal(TimeSpan.FromSeconds(1), DespachanteOutbox.EsperaAteAProximaRodada(new ResultadoLote(3, 3, null), 0, opcoes));
    }

    private DespachanteOutbox Despachante() => new(
        null!,
        null!,
        Options.Create(new MensageriaOptions()),
        (TelemetriaMensageria)_metricas.Instancia,
        TimeProvider.System,
        NullLogger<DespachanteOutbox>.Instance);

    /// <summary>Devolve os resultados em sequência e, depois deles, confirma.</summary>
    private sealed class PublicadorRoteirizado(params ResultadoPublicacao[] roteiro) : IPublicadorConfirmado
    {
        private int _proximo;

        public List<(string Exchange, string RoutingKey, string? MessageId)> Chamadas { get; } = [];

        public Task<ResultadoPublicacao> PublicarAsync(string exchange, string routingKey, BasicProperties propriedades, ReadOnlyMemory<byte> corpo, CancellationToken cancellationToken)
        {
            Chamadas.Add((exchange, routingKey, propriedades.MessageId));
            var resultado = _proximo < roteiro.Length ? roteiro[_proximo] : ResultadoPublicacao.Confirmado;
            _proximo++;
            return Task.FromResult(resultado);
        }
    }
}
