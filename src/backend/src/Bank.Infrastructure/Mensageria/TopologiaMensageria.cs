using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>Uma fila principal do Bank e o que deriva dela (DLQ e, se houver consumidor, as filas de espera).</summary>
/// <param name="NomeConsumidor">Valor de <c>eventos_processados.nome_consumidor</c>.</param>
internal sealed record DescritorFila(
    string NomeConsumidor,
    string Fila,
    string RoutingKey,
    string RoutingKeyDlq,
    string FilaDlq,
    bool ComFilasEspera);

/// <summary>
/// Exchanges, filas e bindings (specs/010-mensageria-rabbitmq, FR-006, research R5;
/// contracts/topologia-e-mensagens.md), todos duráveis e declarados de forma idempotente. O
/// despachante e os consumidores usam a mesma função, então as declarações nunca divergem.
/// </summary>
internal static class TopologiaMensageria
{
    public const string ExchangeEventos = "banco.eventos.topic";
    public const string ExchangeDlx = "banco.eventos.dlx";

    private const string SufixoFila = ".fila";

    public static readonly DescritorFila Transferencias = new(
        "notificacoes-transferencias",
        "notificacoes.transferencias.fila",
        "transferencia.concluida",
        "transferencia.concluida.dlq",
        "notificacoes.transferencias.dlq",
        ComFilasEspera: true);

    /// <summary>Preparação para a Fase 4: sem consumidor, portanto sem filas de espera.</summary>
    public static readonly DescritorFila Cobrancas = new(
        "notificacoes-cobrancas",
        "notificacoes.cobrancas.fila",
        "cobranca.paga",
        "cobranca.paga.dlq",
        "notificacoes.cobrancas.dlq",
        ComFilasEspera: false);

    public static IReadOnlyList<DescritorFila> Filas { get; } = [Transferencias, Cobrancas];

    /// <summary><c>notificacoes.transferencias.fila</c> + 1000 → <c>notificacoes.transferencias.espera.1000</c>.</summary>
    public static string FilaEspera(string fila, int intervaloMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fila);
        var raiz = fila.EndsWith(SufixoFila, StringComparison.Ordinal) ? fila[..^SufixoFila.Length] : fila;
        return $"{raiz}.espera.{intervaloMs}";
    }

    public static Dictionary<string, object?> ArgumentosFilaPrincipal(DescritorFila descritor)
    {
        ArgumentNullException.ThrowIfNull(descritor);
        return new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = ExchangeDlx,
            ["x-dead-letter-routing-key"] = descritor.RoutingKeyDlq,
        };
    }

    /// <summary>Ao expirar o TTL, a mensagem volta pela exchange padrão para a fila principal.</summary>
    public static Dictionary<string, object?> ArgumentosFilaEspera(DescritorFila descritor, int intervaloMs)
    {
        ArgumentNullException.ThrowIfNull(descritor);
        return new Dictionary<string, object?>
        {
            ["x-message-ttl"] = intervaloMs,
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = descritor.Fila,
        };
    }

    public static async Task DeclararAsync(IChannel canal, IReadOnlyList<int> intervalosMs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canal);
        ArgumentNullException.ThrowIfNull(intervalosMs);

        await canal.ExchangeDeclareAsync(ExchangeEventos, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await canal.ExchangeDeclareAsync(ExchangeDlx, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        foreach (var descritor in Filas)
        {
            await canal.QueueDeclareAsync(descritor.Fila, durable: true, exclusive: false, autoDelete: false,
                ArgumentosFilaPrincipal(descritor), cancellationToken: cancellationToken);
            await canal.QueueBindAsync(descritor.Fila, ExchangeEventos, descritor.RoutingKey, cancellationToken: cancellationToken);

            await canal.QueueDeclareAsync(descritor.FilaDlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
            await canal.QueueBindAsync(descritor.FilaDlq, ExchangeDlx, descritor.RoutingKeyDlq, cancellationToken: cancellationToken);

            if (!descritor.ComFilasEspera)
            {
                continue;
            }

            foreach (var intervalo in intervalosMs)
            {
                await canal.QueueDeclareAsync(FilaEspera(descritor.Fila, intervalo), durable: true, exclusive: false, autoDelete: false,
                    ArgumentosFilaEspera(descritor, intervalo), cancellationToken: cancellationToken);
            }
        }
    }
}
