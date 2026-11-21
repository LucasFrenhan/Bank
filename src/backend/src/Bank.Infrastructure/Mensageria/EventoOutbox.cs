using System.Text.Json;
using Bank.Domain.Comum;
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

    public EventoId Id { get; private set; }

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
            Payload = JsonSerializer.Serialize(PayloadTransferenciaConcluida.De(evento), Json),
            OcorridoEm = evento.OcorridoEm,
        };
    }

    /// <summary>
    /// Forma do payload v1 (contracts/evento-transferencia-concluida.md): identificadores como
    /// <see cref="Guid"/>, independentes dos tipos do Domain (issue #43).
    /// </summary>
    private sealed record PayloadTransferenciaConcluida(
        Guid EventoId,
        Guid TransferenciaId,
        Guid PagadorUsuarioId,
        Guid RecebedorUsuarioId,
        decimal Valor,
        DateTimeOffset OcorridoEm)
    {
        public static PayloadTransferenciaConcluida De(TransferenciaConcluida evento) => new(
            evento.EventoId.Valor,
            evento.TransferenciaId.Valor,
            evento.PagadorUsuarioId.Valor,
            evento.RecebedorUsuarioId.Valor,
            evento.Valor,
            evento.OcorridoEm);
    }
}
