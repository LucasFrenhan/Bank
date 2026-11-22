using Bank.Domain.Comum;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Marca que um consumidor já tratou um evento (tabela <c>eventos_processados</c>;
/// specs/010-mensageria-rabbitmq, FR-019, data-model). Gravado só por SQL
/// (<c>RegistrarEventoProcessado.sql</c>, com <c>ON CONFLICT DO NOTHING</c>); a entidade existe
/// para o mapeamento, as migrations e as asserções dos testes. Imutável.
/// </summary>
public sealed class EventoProcessado
{
    private EventoProcessado()
    {
    }

    public EventoId EventoId { get; private set; }

    public string NomeConsumidor { get; private set; } = string.Empty;

    public DateTimeOffset ProcessadoEm { get; private set; }
}
