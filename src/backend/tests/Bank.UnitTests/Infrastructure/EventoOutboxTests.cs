using System.Text.Json;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Contrato do evento <c>transferencia.concluida</c> v1 (constituição, Princípio VII: testes de
/// serialização e conformidade de schema; specs/006-transferencias, contracts).
/// </summary>
public class EventoOutboxTests
{
    [Fact]
    public void De_TransferenciaConcluida_SegueOSchemaV1()
    {
        var evento = new TransferenciaConcluida(
            EventoId.De(Guid.CreateVersion7()),
            TransferenciaId.De(Guid.CreateVersion7()),
            UsuarioId.De(Guid.CreateVersion7()),
            UsuarioId.De(Guid.CreateVersion7()),
            150.50m,
            new DateTimeOffset(2026, 11, 17, 18, 30, 0, TimeSpan.Zero));

        var outbox = EventoOutbox.De(evento);

        Assert.Equal(("transferencia.concluida", 1, evento.EventoId, evento.OcorridoEm), (outbox.Tipo, outbox.Versao, outbox.Id, outbox.OcorridoEm));
        Assert.Null(outbox.PublicadoEm);

        using var payload = JsonDocument.Parse(outbox.Payload);
        var campos = payload.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["eventoId", "ocorridoEm", "pagadorUsuarioId", "recebedorUsuarioId", "transferenciaId", "valor"], campos);
        Assert.Equal(evento.TransferenciaId.Valor, payload.RootElement.GetProperty("transferenciaId").GetGuid());
        Assert.Equal("150.50", payload.RootElement.GetProperty("valor").GetRawText());
        Assert.Equal(TimeSpan.Zero, payload.RootElement.GetProperty("ocorridoEm").GetDateTimeOffset().Offset);
    }
}
