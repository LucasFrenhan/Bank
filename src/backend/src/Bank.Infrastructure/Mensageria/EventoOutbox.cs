using System.Text.Json;
using Bank.Domain.Transferencias;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Evento de integração gravado na mesma transação da operação (Transactional Outbox —
/// constituição, Princípio IV; specs/006-transferencias, research R9). A publicação no RabbitMQ
/// (TASK-09) preencherá <see cref="PublicadoEm"/>.
/// </summary>
public sealed class EventoOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private EventoOutbox()
    {
    }

    public Guid Id { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    public int Versao { get; private set; }

    /// <summary>Payload JSON (camelCase) conforme contracts/evento-transferencia-concluida.md.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OcorridoEm { get; private set; }

    public DateTimeOffset? PublicadoEm { get; private set; }

    public static EventoOutbox De(TransferenciaConcluida evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return new EventoOutbox
        {
            Id = evento.EventoId,
            Tipo = TransferenciaConcluida.Tipo,
            Versao = TransferenciaConcluida.Versao,
            Payload = JsonSerializer.Serialize(evento, Json),
            OcorridoEm = evento.OcorridoEm,
        };
    }
}
