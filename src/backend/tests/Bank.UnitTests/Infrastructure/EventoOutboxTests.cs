using System.Diagnostics;
using Bank.Infrastructure.Mensageria;

namespace Bank.UnitTests.Infrastructure;

/// <summary>
/// Registro do outbox (specs/006-transferencias, research R9) e contexto de trace guardado na
/// gravação (specs/010-mensageria-rabbitmq, FR-005, research R6). O conteúdo do payload é coberto
/// por <see cref="ContratoEventosTests"/>.
/// </summary>
public class EventoOutboxTests
{
    [Fact]
    public void De_ComAtividadeW3C_GuardaTraceparentETracestate()
    {
        using var atividade = new Activity("teste").SetIdFormat(ActivityIdFormat.W3C).Start();
        atividade.TraceStateString = "bank=1";

        var outbox = EventoOutbox.De(ContratoEventosTests.Evento());

        Assert.Equal(atividade.Id, outbox.TraceParent);
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", outbox.TraceParent!);
        Assert.Equal("bank=1", outbox.TraceState);
    }

    [Fact]
    public void De_SemAtividade_SemContexto()
    {
        Activity.Current = null;

        var outbox = EventoOutbox.De(ContratoEventosTests.Evento());

        Assert.Null(outbox.TraceParent);
        Assert.Null(outbox.TraceState);
    }

    [Fact]
    public void MarcarPublicado_SoPreencheUmaVez()
    {
        var outbox = EventoOutbox.De(ContratoEventosTests.Evento());
        var primeira = DateTimeOffset.UtcNow;

        outbox.MarcarPublicado(primeira);
        outbox.MarcarPublicado(primeira.AddMinutes(1));

        Assert.Equal(primeira, outbox.PublicadoEm);
    }
}
