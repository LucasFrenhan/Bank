using System.Diagnostics;
using System.Text.Json;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Infrastructure.Mensageria.Contratos;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Evento de integração gravado na mesma transação da operação (Transactional Outbox —
/// constituição, Princípio IV; specs/006-transferencias, research R9). A publicação no RabbitMQ
/// é feita pelo despachante do Worker, que preenche <see cref="PublicadoEm"/> após a confirmação do
/// broker (specs/010-mensageria-rabbitmq, research R4).
/// </summary>
public sealed class EventoOutbox
{
    private EventoOutbox()
    {
    }

    public EventoId Id { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    public int Versao { get; private set; }

    /// <summary>Payload JSON (camelCase) conforme contracts/ do evento.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OcorridoEm { get; private set; }

    public DateTimeOffset? PublicadoEm { get; private set; }

    /// <summary>
    /// <c>traceparent</c> W3C ativo na gravação, para o despachante continuar o trace da operação
    /// mesmo publicando depois (specs/010-mensageria-rabbitmq, FR-005, research R6).
    /// </summary>
    public string? TraceParent { get; private set; }

    /// <summary><c>tracestate</c> W3C ativo na gravação, quando houver.</summary>
    public string? TraceState { get; private set; }

    /// <summary>Única alteração permitida depois da gravação: a confirmação do broker (FR-002).</summary>
    public void MarcarPublicado(DateTimeOffset publicadoEm)
    {
        PublicadoEm ??= publicadoEm;
    }

    /// <summary>
    /// Registro do outbox com o payload v1 revisado (specs/010-mensageria-rabbitmq, FR-011) e o
    /// contexto de trace ativo (FR-005): o <c>traceparent</c> só é guardado no formato W3C.
    /// </summary>
    public static EventoOutbox De(TransferenciaConcluida evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return Novo(evento.EventoId, TransferenciaConcluida.Tipo, TransferenciaConcluida.Versao,
            JsonSerializer.Serialize(TransferenciaConcluidaV1.De(evento), TransferenciaConcluidaV1.Json), evento.OcorridoEm);
    }

    /// <summary>Evento <c>cobranca.paga</c> v1 (specs/014-cartao-credito-cobrancas, FR-021).</summary>
    public static EventoOutbox De(CobrancaPaga evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return Novo(evento.EventoId, CobrancaPaga.Tipo, CobrancaPaga.Versao, CobrancaPagaV1.De(evento).Serializar(), evento.OcorridoEm);
    }

    private static EventoOutbox Novo(EventoId id, string tipo, int versao, string payload, DateTimeOffset ocorridoEm)
    {
        var atividade = Activity.Current;
        var w3c = atividade is { IdFormat: ActivityIdFormat.W3C };

        return new EventoOutbox
        {
            Id = id,
            Tipo = tipo,
            Versao = versao,
            Payload = payload,
            OcorridoEm = ocorridoEm,
            TraceParent = w3c ? atividade!.Id : null,
            TraceState = w3c && !string.IsNullOrEmpty(atividade!.TraceStateString) ? atividade.TraceStateString : null,
        };
    }
}
