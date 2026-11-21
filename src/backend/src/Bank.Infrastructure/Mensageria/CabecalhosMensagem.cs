using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Propriedades e headers AMQP das mensagens (specs/010-mensageria-rabbitmq, FR-010;
/// contracts/topologia-e-mensagens.md) e propagação do contexto W3C (RN-06, research R6).
/// </summary>
internal static class CabecalhosMensagem
{
    public const string ContentType = "application/json";
    public const string ContentEncoding = "utf-8";
    public const string Versao = "versao";

    /// <summary>
    /// Mesmo propagador que o SDK do OpenTelemetry instala como padrão (TraceContext + Baggage),
    /// fixado aqui para não depender de o SDK já ter sido inicializado no processo.
    /// </summary>
    private static readonly TextMapPropagator Propagador =
        new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]);

    /// <summary>Propriedades da publicação de um evento do outbox.</summary>
    public static BasicProperties ParaPublicacao(EventoOutbox evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return new BasicProperties
        {
            ContentType = ContentType,
            ContentEncoding = ContentEncoding,
            Persistent = true,
            MessageId = evento.Id.Valor.ToString(),
            Type = evento.Tipo,
            Timestamp = new AmqpTimestamp(evento.OcorridoEm.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?> { [Versao] = evento.Versao },
        };
    }

    /// <summary>Cópia das propriedades originais com outros headers (espera e DLQ, FR-018).</summary>
    public static BasicProperties Copiar(IReadOnlyBasicProperties original, IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(original);

        return new BasicProperties
        {
            ContentType = original.ContentType,
            ContentEncoding = original.ContentEncoding,
            Persistent = true,
            MessageId = original.MessageId,
            Type = original.Type,
            Timestamp = original.Timestamp,
            Headers = headers,
        };
    }

    /// <summary>Injeta o contexto (e a baggage atual) como <c>traceparent</c>/<c>tracestate</c> em texto.</summary>
    public static void InjetarContexto(ActivityContext contexto, IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (contexto == default)
        {
            return;
        }

        Propagador.Inject(new PropagationContext(contexto, Baggage.Current), headers, static (h, chave, valor) => h[chave] = valor);
    }

    /// <summary>Contexto dos headers; vazio (<c>default</c>) se ausente ou inválido.</summary>
    public static ActivityContext ExtrairContexto(IDictionary<string, object?>? headers)
    {
        if (headers is null)
        {
            return default;
        }

        var contexto = Propagador.Extract(default, headers, static (h, chave) =>
            LerTexto(h, chave) is { } valor ? [valor] : []);
        return contexto.ActivityContext;
    }

    /// <summary>Header em texto: o cliente entrega strings AMQP como <c>byte[]</c>.</summary>
    public static string? LerTexto(IDictionary<string, object?>? headers, string chave) =>
        headers is not null && headers.TryGetValue(chave, out var valor)
            ? valor switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string texto => texto,
                null => null,
                _ => Convert.ToString(valor, CultureInfo.InvariantCulture),
            }
            : null;

    /// <summary>Header inteiro, aceitando os tipos numéricos AMQP e texto.</summary>
    public static int? LerInteiro(IDictionary<string, object?>? headers, string chave)
    {
        if (headers is null || !headers.TryGetValue(chave, out var valor) || valor is null)
        {
            return null;
        }

        return valor switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            short s => s,
            byte b => b,
            sbyte sb => sb,
            uint ui when ui <= int.MaxValue => (int)ui,
            byte[] or string => int.TryParse(LerTexto(headers, chave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
            _ => null,
        };
    }
}
