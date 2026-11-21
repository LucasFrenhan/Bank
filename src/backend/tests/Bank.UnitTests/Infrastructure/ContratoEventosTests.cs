using System.Text.Json;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Conformidade dos contratos de mensageria (constituição, Princípio VII; specs/010-mensageria-rabbitmq,
/// FR-011, FR-025; contracts/evento-transferencia-concluida.md).
/// </summary>
public class ContratoEventosTests
{
    private static readonly DateTimeOffset OcorridoEm = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);

    internal static TransferenciaConcluida Evento(string? descricao = "Pagamento almoço") => new(
        EventoId.De(Guid.CreateVersion7()),
        TransferenciaId.De(Guid.CreateVersion7()),
        UsuarioId.De(Guid.CreateVersion7()),
        UsuarioId.De(Guid.CreateVersion7()),
        150.50m,
        OcorridoEm,
        "Lucas Silva",
        "Cafeteria Grão Especial LTDA",
        descricao);

    [Fact]
    public void TransferenciaConcluida_SegueOContratoV1Revisado()
    {
        var evento = Evento();

        var outbox = EventoOutbox.De(evento);

        Assert.Equal(("transferencia.concluida", 1, evento.EventoId, evento.OcorridoEm), (outbox.Tipo, outbox.Versao, outbox.Id, outbox.OcorridoEm));
        Assert.Null(outbox.PublicadoEm);

        using var payload = JsonDocument.Parse(outbox.Payload);
        var raiz = payload.RootElement;
        Assert.Equal(
            ["eventoId", "tipoEvento", "versao", "transferenciaId", "pagadorUsuarioId", "pagadorNome", "recebedorUsuarioId", "recebedorNome", "valor", "descricao", "ocorridoEm"],
            raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal(evento.EventoId.Valor, raiz.GetProperty("eventoId").GetGuid());
        Assert.Equal("TransferenciaConcluida", raiz.GetProperty("tipoEvento").GetString());
        Assert.Equal(1, raiz.GetProperty("versao").GetInt32());
        Assert.Equal(evento.TransferenciaId.Valor, raiz.GetProperty("transferenciaId").GetGuid());
        Assert.Equal(evento.PagadorUsuarioId.Valor, raiz.GetProperty("pagadorUsuarioId").GetGuid());
        Assert.Equal("Lucas Silva", raiz.GetProperty("pagadorNome").GetString());
        Assert.Equal(evento.RecebedorUsuarioId.Valor, raiz.GetProperty("recebedorUsuarioId").GetGuid());
        Assert.Equal("Cafeteria Grão Especial LTDA", raiz.GetProperty("recebedorNome").GetString());
        Assert.Equal("150.50", raiz.GetProperty("valor").GetRawText());
        Assert.Equal("Pagamento almoço", raiz.GetProperty("descricao").GetString());
        Assert.Equal(OcorridoEm, raiz.GetProperty("ocorridoEm").GetDateTimeOffset());
        Assert.Equal(TimeSpan.Zero, raiz.GetProperty("ocorridoEm").GetDateTimeOffset().Offset);
    }

    [Fact]
    public void TransferenciaConcluida_SemDescricao_SerializaNull()
    {
        using var payload = JsonDocument.Parse(EventoOutbox.De(Evento(descricao: null)).Payload);

        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("descricao").ValueKind);
    }
}
