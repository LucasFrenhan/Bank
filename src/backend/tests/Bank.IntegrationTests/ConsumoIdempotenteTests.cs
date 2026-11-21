using System.Net;
using Bank.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Entrega duplicada não repete a notificação (specs/010-mensageria-rabbitmq, US3: FR-017; SC-003)
/// e dois despachantes não publicam o mesmo evento (FR-004).
/// </summary>
public class ConsumoIdempotenteTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static string Payload(Guid eventoId) => $$"""
        {"eventoId":"{{eventoId}}","tipoEvento":"TransferenciaConcluida","versao":1,
         "transferenciaId":"{{Guid.CreateVersion7()}}","pagadorUsuarioId":"{{Guid.CreateVersion7()}}","pagadorNome":"Pagador",
         "recebedorUsuarioId":"{{Guid.CreateVersion7()}}","recebedorNome":"Recebedor","valor":1.00,"descricao":null,
         "ocorridoEm":"2026-11-21T12:00:00+00:00"}
        """;

    [Fact]
    public async Task MesmaMensagemDuasVezes_UmaNotificacao()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        var eventoId = Guid.CreateVersion7();
        var corpo = Payload(eventoId);

        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, corpo, eventoId.ToString(), cancellationToken: Ct);
            Assert.True(await BankWorkerFactory.AguardarAsync(() => ContarProcessadosAsync(eventoId).ContinueWith(t => t.Result == 1, TaskScheduler.Default)));
            await BankWorkerFactory.PublicarAsync(canal, corpo, eventoId.ToString(), cancellationToken: Ct);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() => Medicoes(worker, "duplicado") >= 1));
        Assert.Equal(1, await ContarProcessadosAsync(eventoId));
        Assert.Equal(2, ContarNotificacoes(worker, eventoId));
        await AssertFilaVaziaAsync();
    }

    [Fact]
    public async Task CopiasSimultaneasEmDoisWorkers_UmaNotificacao()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        await using var segundo = new BankWorkerFactory(ambiente.Api.ConnectionString, worker.Broker);
        segundo.Iniciar();
        await AguardarProntoAsync(worker);
        await AguardarProntoAsync(segundo);

        var eventoId = Guid.CreateVersion7();
        var corpo = Payload(eventoId);
        var duplicadosAntes = Medicoes(worker, "duplicado") + Medicoes(segundo, "duplicado");

        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => BankWorkerFactory.PublicarAsync(canal, corpo, eventoId.ToString(), cancellationToken: Ct)));
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            Medicoes(worker, "duplicado") + Medicoes(segundo, "duplicado") - duplicadosAntes == 4));
        Assert.Equal(1, await ContarProcessadosAsync(eventoId));
        Assert.Equal(2, ContarNotificacoes(worker, eventoId) + ContarNotificacoes(segundo, eventoId));
        await AssertFilaVaziaAsync();
    }

    [Fact]
    public async Task DoisDespachantes_CadaEventoPublicadoUmaVez()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        await using var segundo = new BankWorkerFactory(ambiente.Api.ConnectionString, worker.Broker);
        segundo.Iniciar();
        await AguardarProntoAsync(segundo);

        const string espia = "teste.espia.despachantes";
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await canal.QueueDeclareAsync(espia, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
            await canal.QueueBindAsync(espia, "banco.eventos.topic", "transferencia.concluida", cancellationToken: Ct);
            await canal.QueuePurgeAsync(espia, Ct);
        }

        var publicadosAntes = worker.Somar("banco.eventos.publicados") + segundo.Somar("banco.eventos.publicados");

        using var client = ambiente.Api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        for (var i = 0; i < 10; i++)
        {
            using var resposta = await TransferirAsync(client, pf, pj.Documento, 0.10m);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            worker.Somar("banco.eventos.publicados") + segundo.Somar("banco.eventos.publicados") - publicadosAntes >= 10), "Os 10 eventos deveriam ser publicados.");
        await Task.Delay(1000, Ct); // tempo para uma eventual publicação dupla aparecer

        Assert.Equal(10, worker.Somar("banco.eventos.publicados") + segundo.Somar("banco.eventos.publicados") - publicadosAntes);
        var ids = new List<string>();
        (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            while (await canal.BasicGetAsync(espia, autoAck: true, Ct) is { } mensagem)
            {
                ids.Add(mensagem.BasicProperties.MessageId!);
            }
        }

        Assert.Equal(10, ids.Count);
        Assert.Equal(10, ids.Distinct(StringComparer.Ordinal).Count());
    }

    private static double Medicoes(BankWorkerFactory worker, string status) =>
        worker.Medicoes.Count(m => m.Nome == "banco.eventos.processados" && m.Tags["status"] == status);

    private static int ContarNotificacoes(BankWorkerFactory worker, Guid eventoId) =>
        worker.TextosDosLogs().Count(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("Notificação de transferência", StringComparison.Ordinal));

    private async Task<int> ContarProcessadosAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        return await contexto.EventosProcessados.CountAsync(e => e.EventoId == EventoId.De(eventoId), Ct);
    }

    private async Task AssertFilaVaziaAsync()
    {
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            var fila = await ambiente.Worker.Broker.ObterFilaAsync("notificacoes.transferencias.fila", Ct);
            return fila.TryGetProperty("messages", out var mensagens) && mensagens.GetInt32() == 0;
        }), "Todas as entregas deveriam ser confirmadas.");
    }

    private static async Task AguardarProntoAsync(BankWorkerFactory worker)
    {
        using var saude = worker.CreateClient();
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            using var resposta = await saude.GetAsync(new Uri("/health/pronto", UriKind.Relative), Ct);
            return resposta.StatusCode == HttpStatusCode.OK;
        }));
    }
}
