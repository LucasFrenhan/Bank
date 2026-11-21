using System.Diagnostics;
using System.Text;
using Bank.Infrastructure.Mensageria;
using RabbitMQ.Client;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Propriedades AMQP (specs/010-mensageria-rabbitmq, FR-010; contracts/topologia-e-mensagens.md) e
/// propagação W3C (RN-06, FR-020, FR-021).
/// </summary>
public class CabecalhosMensagemTests
{
    [Fact]
    public void ParaPublicacao_SegueOContrato()
    {
        var evento = ContratoEventosTests.Evento();
        var outbox = EventoOutbox.De(evento);

        var propriedades = CabecalhosMensagem.ParaPublicacao(outbox);

        Assert.Equal(("application/json", "utf-8", DeliveryModes.Persistent), (propriedades.ContentType, propriedades.ContentEncoding, propriedades.DeliveryMode));
        Assert.Equal(evento.EventoId.Valor.ToString(), propriedades.MessageId);
        Assert.Equal("transferencia.concluida", propriedades.Type);
        Assert.Equal(evento.OcorridoEm.ToUnixTimeSeconds(), propriedades.Timestamp.UnixTime);
        Assert.Equal(1, propriedades.Headers!["versao"]);
    }

    [Fact]
    public void Copiar_PreservaIdentidadeETrocaHeaders()
    {
        var original = new BasicProperties { ContentType = "application/json", ContentEncoding = "utf-8", MessageId = "m1", Type = "t", Timestamp = new AmqpTimestamp(10), Persistent = true };
        var headers = new Dictionary<string, object?> { ["x"] = 1 };

        var copia = CabecalhosMensagem.Copiar(original, headers);

        Assert.Equal(("application/json", "utf-8", "m1", "t", 10L, true), (copia.ContentType, copia.ContentEncoding, copia.MessageId, copia.Type, copia.Timestamp.UnixTime, copia.Persistent));
        Assert.Same(headers, copia.Headers);
    }

    [Fact]
    public void InjetarEExtrair_PreservamOContexto()
    {
        var contexto = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "bank=1");
        var headers = new Dictionary<string, object?>();

        CabecalhosMensagem.InjetarContexto(contexto, headers);

        Assert.Equal($"00-{contexto.TraceId}-{contexto.SpanId}-01", Assert.IsType<string>(headers["traceparent"]));
        Assert.Equal("bank=1", headers["tracestate"]);

        // Como o cliente entrega: strings AMQP chegam como byte[].
        var recebidos = headers.ToDictionary(h => h.Key, h => (object?)Encoding.UTF8.GetBytes((string)h.Value!));
        var extraido = CabecalhosMensagem.ExtrairContexto(recebidos);
        Assert.Equal((contexto.TraceId, contexto.SpanId, "bank=1"), (extraido.TraceId, extraido.SpanId, extraido.TraceState));
    }

    [Fact]
    public void Extrair_SemHeaders_ContextoVazio()
    {
        Assert.Equal(default, CabecalhosMensagem.ExtrairContexto(null));
        Assert.Equal(default, CabecalhosMensagem.ExtrairContexto(new Dictionary<string, object?>()));
        Assert.Equal(default, CabecalhosMensagem.ExtrairContexto(new Dictionary<string, object?> { ["traceparent"] = "lixo" }));
    }

    [Fact]
    public void Injetar_ContextoVazio_NaoEscreveNada()
    {
        var headers = new Dictionary<string, object?>();

        CabecalhosMensagem.InjetarContexto(default, headers);

        Assert.Empty(headers);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(3L)]
    [InlineData((short)3)]
    [InlineData((byte)3)]
    [InlineData("3")]
    public void LerInteiro_AceitaOsTiposAmqp(object valor)
    {
        Assert.Equal(3, CabecalhosMensagem.LerInteiro(new Dictionary<string, object?> { ["n"] = valor }, "n"));
    }
}
