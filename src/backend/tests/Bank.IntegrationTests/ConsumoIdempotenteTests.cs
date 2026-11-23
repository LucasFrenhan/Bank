using System.Net;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
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

    /// <summary>
    /// Payload v1 publicado à mão. Pagador e recebedor precisam existir em <c>usuarios</c>: as
    /// notificações gravadas os referenciam por chave estrangeira (spec 011; <see cref="UsuariosDeTeste"/>).
    /// </summary>
    internal static string Payload(Guid eventoId, Guid pagadorUsuarioId, Guid recebedorUsuarioId) => $$"""
        {"eventoId":"{{eventoId}}","tipoEvento":"TransferenciaConcluida","versao":1,
         "transferenciaId":"{{Guid.CreateVersion7()}}","pagadorUsuarioId":"{{pagadorUsuarioId}}","pagadorNome":"Pagador",
         "recebedorUsuarioId":"{{recebedorUsuarioId}}","recebedorNome":"Recebedor","valor":1.00,"descricao":null,
         "ocorridoEm":"2026-11-21T12:00:00+00:00"}
        """;

    [Fact]
    public async Task MesmaMensagemDuasVezes_UmaNotificacao()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        var (pagador, recebedor) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        var corpo = Payload(eventoId, pagador, recebedor);

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
        await AssertUmParGravadoAsync(eventoId);
        await AssertFilaVaziaAsync();
    }

    /// <summary>
    /// Defeito simulado na 1ª camada (spec 011, research R3): o par já está gravado, mas o evento
    /// não está em <c>eventos_processados</c>. O caso de uso não grava de novo.
    /// </summary>
    [Fact]
    public async Task NotificacoesJaGravadas_SemEventoProcessado_NaoDuplica()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        var (pagador, recebedor) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        await using (var contexto = ambiente.Api.CriarContexto())
        {
            var agora = DateTimeOffset.UtcNow;
            contexto.NotificacoesAuditoria.AddRange(
                NotificacaoAuditoria.ComprovanteEnvio(EventoId.De(eventoId), UsuarioId.De(pagador), 1m, "Recebedor", agora),
                NotificacaoAuditoria.AvisoCredito(EventoId.De(eventoId), UsuarioId.De(recebedor), 1m, "Pagador", agora));
            await contexto.SaveChangesAsync(Ct);
        }

        var sucessosAntes = Medicoes(worker, "sucesso");
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, Payload(eventoId, pagador, recebedor), eventoId.ToString(), cancellationToken: Ct);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(eventoId) == 1),
            "O evento deveria ser processado sem nova tentativa nem DLQ.");
        Assert.True(await BankWorkerFactory.AguardarAsync(() => Medicoes(worker, "sucesso") - sucessosAntes >= 1));
        await AssertUmParGravadoAsync(eventoId);
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
        var (pagador, recebedor) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        var corpo = Payload(eventoId, pagador, recebedor);
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
        await AssertUmParGravadoAsync(eventoId);
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

    /// <summary>Exatamente 2 notificações persistidas, uma de cada tipo (spec 011, FR-006; SC-003).</summary>
    private async Task AssertUmParGravadoAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        var tipos = await contexto.NotificacoesAuditoria
            .Where(n => n.EventoId == EventoId.De(eventoId))
            .Select(n => n.Tipo)
            .ToListAsync(Ct);
        Assert.Equal([TipoNotificacao.ComprovanteEnvio, TipoNotificacao.AvisoCredito], tipos.Order());
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
