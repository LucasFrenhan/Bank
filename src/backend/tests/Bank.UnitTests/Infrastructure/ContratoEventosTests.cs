using System.Text.Json;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria;
using Bank.SharedKernel;

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

/// <summary>Contrato <c>cobranca.paga</c> v1, preparação da Fase 4 (specs/010-mensageria-rabbitmq, FR-012).</summary>
public class ContratoCobrancaPagaTests
{
    [Fact]
    public void CobrancaPaga_SegueOContratoV1()
    {
        var evento = new Bank.Infrastructure.Mensageria.Contratos.CobrancaPagaV1(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 299.90m, "4242",
            new DateTimeOffset(2026, 11, 17, 15, 10, 0, TimeSpan.Zero));

        using var payload = JsonDocument.Parse(evento.Serializar());
        var raiz = payload.RootElement;
        Assert.Equal(
            ["eventoId", "tipoEvento", "versao", "cobrancaId", "lojistaUsuarioId", "pagadorUsuarioId", "valor", "ultimosDigitosCartao", "ocorridoEm"],
            raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("CobrancaPaga", 1, "299.90", "4242"),
            (raiz.GetProperty("tipoEvento").GetString(), raiz.GetProperty("versao").GetInt32(), raiz.GetProperty("valor").GetRawText(), raiz.GetProperty("ultimosDigitosCartao").GetString()));
    }

    [Theory]
    [InlineData("424")]
    [InlineData("42424")]
    [InlineData("4242424242424242")]
    [InlineData("42a2")]
    public void CobrancaPaga_SoAceitaOsQuatroUltimosDigitos(string digitos)
    {
        Assert.Throws<ArgumentException>(() => new Bank.Infrastructure.Mensageria.Contratos.CobrancaPagaV1(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1m, digitos, DateTimeOffset.UtcNow));
    }
}
