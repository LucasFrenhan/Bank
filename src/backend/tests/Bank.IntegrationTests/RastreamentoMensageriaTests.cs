using System.Diagnostics;
using System.Net;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Spans da transferência até a notificação, para os testes de trace.</summary>
internal static class SpansMensageria
{
    public const string Producer = "banco.eventos.topic publish";
    public const string Consumer = "notificacoes.transferencias.fila process";

    /// <summary>
    /// Span HTTP do <c>POST /transferencias</c> no trace dado. Os tracer providers de todas as APIs de
    /// teste do processo ouvem a mesma fonte do ASP.NET Core, então o span é achado pelo trace.
    /// </summary>
    public static Activity? DaRequisicao(AmbienteMensageria ambiente, ActivityTraceId traceId) =>
        ambiente.Api.Spans.Copiar().SingleOrDefault(a => a.Kind == ActivityKind.Server && a.TraceId == traceId
            && Equals(a.GetTagItem("url.path"), "/api/v1/transferencias") && Equals(a.GetTagItem("http.request.method"), "POST"));

    public static Activity? DoEvento(BankWorkerFactory worker, string nome, string eventoId) =>
        worker.Spans.Copiar().FirstOrDefault(a => a.DisplayName == nome && Equals(a.GetTagItem("messaging.message.id"), eventoId));

    public static async Task<string> TransferirAsync(AmbienteMensageria ambiente)
    {
        using var client = ambiente.Api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        using var resposta = await ApiTransferencias.TransferirAsync(client, pf, pj.Documento, 0.10m);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        await using var contexto = ambiente.Api.CriarContexto();
        return contexto.EventosOutbox.AsEnumerable()
            .Single(e => e.Payload.Contains(pf.UsuarioId.ToString(), StringComparison.Ordinal)).Id.Valor.ToString();
    }

    /// <summary>Requisição → Producer → Consumer no mesmo trace, com a cadeia de pais (FR-020, FR-021).</summary>
    public static async Task AssertTraceUnicoAsync(AmbienteMensageria ambiente, string eventoId)
    {
        var worker = ambiente.Worker;
        Assert.True(await BankWorkerFactory.AguardarAsync(() => DoEvento(worker, Consumer, eventoId) is not null), "Span de consumo não exportado.");

        var producer = DoEvento(worker, Producer, eventoId);
        Assert.NotNull(producer);
        var requisicao = DaRequisicao(ambiente, producer.TraceId);
        var consumer = DoEvento(worker, Consumer, eventoId);
        Assert.NotNull(requisicao);
        Assert.NotNull(producer);
        Assert.NotNull(consumer);

        Assert.Equal(ActivityKind.Producer, producer.Kind);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
        Assert.Equal(requisicao.TraceId, producer.TraceId);
        Assert.Equal(requisicao.TraceId, consumer.TraceId);
        Assert.Equal(producer.SpanId, consumer.ParentSpanId);

        Assert.Equal("rabbitmq", producer.GetTagItem("messaging.system"));
        Assert.Equal("send", producer.GetTagItem("messaging.operation.type"));
        Assert.Equal("banco.eventos.topic", producer.GetTagItem("messaging.destination.name"));
        Assert.Equal("transferencia.concluida", producer.GetTagItem("messaging.rabbitmq.destination.routing_key"));
        Assert.Equal("process", consumer.GetTagItem("messaging.operation.type"));
        Assert.Equal("notificacoes.transferencias.fila", consumer.GetTagItem("messaging.destination.name"));
        Assert.Equal("sucesso", consumer.GetTagItem("bank.mensageria.resultado"));
    }
}

/// <summary>Trace ponta a ponta (specs/010-mensageria-rabbitmq, US5: FR-005, FR-020, FR-021; SC-006).</summary>
public class RastreamentoMensageriaTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Transferencia_RequisicaoPublicacaoEConsumoNoMesmoTrace()
    {
        await ambiente.Worker.IniciarProntoAsync();

        var eventoId = await SpansMensageria.TransferirAsync(ambiente);

        await SpansMensageria.AssertTraceUnicoAsync(ambiente, eventoId);
    }

    [Fact]
    public async Task MensagemSemTraceparent_ProcessadaNumTraceNovo()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();

        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, ConsumoIdempotenteTests.Payload(eventoId), eventoId.ToString(), cancellationToken: Ct);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() => SpansMensageria.DoEvento(worker, SpansMensageria.Consumer, eventoId.ToString()) is not null));
        var consumer = SpansMensageria.DoEvento(worker, SpansMensageria.Consumer, eventoId.ToString())!;
        Assert.Equal(default, consumer.ParentSpanId);
        Assert.Equal("sucesso", consumer.GetTagItem("bank.mensageria.resultado"));
    }
}

/// <summary>
/// Publicação atrasada (Worker iniciado depois da transferência, como após uma queda do broker):
/// o contexto guardado no outbox mantém o mesmo trace (FR-005, research R6).
/// </summary>
public class RastreamentoPublicacaoAtrasadaTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    [Fact]
    public async Task PublicacaoAtrasada_ContinuaOTraceDaRequisicao()
    {
        var eventoId = await SpansMensageria.TransferirAsync(ambiente);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        ambiente.Worker.Iniciar();

        await SpansMensageria.AssertTraceUnicoAsync(ambiente, eventoId);
    }
}
