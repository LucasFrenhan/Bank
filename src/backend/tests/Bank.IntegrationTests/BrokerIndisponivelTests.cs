using System.Net;
using Bank.SharedKernel;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>API + Worker com um contêiner RabbitMQ só desta classe (os testes param e religam o broker).</summary>
public sealed class AmbienteMensageriaExclusivo : AmbienteMensageria
{
    private BrokerDeTeste? _broker;

    protected override async Task<BrokerDeTeste> CriarBrokerAsync() => _broker = await RabbitMqCompartilhado.CriarExclusivoAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_broker is not null)
        {
            await _broker.Container.DisposeAsync();
        }
    }
}

/// <summary>
/// Nenhum evento se perde quando o broker falha (specs/010-mensageria-rabbitmq, US2: FR-002,
/// FR-003, FR-008; SC-002, SC-005) e a prontidão do Worker acompanha a conexão (FR-024).
/// </summary>
public class BrokerIndisponivelTests(AmbienteMensageriaExclusivo ambiente) : IClassFixture<AmbienteMensageriaExclusivo>
{
    private const string FilaEspia = "teste.espia.transferencias";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BrokerParado_TransferenciasConcluemEEventosSaemQuandoEleVolta()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        using var client = ambiente.Api.CreateClient();
        using var saude = worker.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);

        // Fila espiã durável, sem consumidor: guarda uma cópia de cada mensagem publicada.
        await PrepararEspiaAsync();
        Assert.True(await ProntoAsync(saude, HttpStatusCode.OK));

        // Uma mensagem persistente parada na espiã antes de reiniciar o broker (SC-005).
        using (var antes = await TransferirAsync(client, pf, pj.Documento, 0.10m))
        {
            Assert.Equal(HttpStatusCode.Created, antes.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(pf) == 1));

        await worker.Broker.Container.StopAsync(Ct);
        Assert.True(await ProntoAsync(saude, HttpStatusCode.ServiceUnavailable), "/health/pronto deveria responder 503 com o broker parado.");

        for (var i = 0; i < 3; i++)
        {
            using var resposta = await TransferirAsync(client, pf, pj.Documento, 0.10m);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        await using (var contexto = ambiente.Api.CriarContexto())
        {
            Assert.Equal(3, await contexto.EventosOutbox.CountAsync(e => e.PublicadoEm == null, Ct));
        }

        await worker.Broker.Container.StartAsync(Ct);

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(pf) == 4, TimeSpan.FromSeconds(60)),
            "Os eventos pendentes não foram publicados e consumidos depois que o broker voltou.");
        Assert.True(await ProntoAsync(saude, HttpStatusCode.OK));

        await using (var contexto = ambiente.Api.CriarContexto())
        {
            Assert.Equal(0, await contexto.EventosOutbox.CountAsync(e => e.PublicadoEm == null, Ct));
        }

        // A espiã guardou as 4 (a de antes sobreviveu ao reinício), na ordem de gravação.
        var esperados = await IdsEmOrdemDeGravacaoAsync(pf);
        Assert.Equal(esperados, await LerEspiaAsync());
    }

    [Fact]
    public async Task PublicacaoRepetida_DepoisDeConfirmada_NaoNotificaDeNovo()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        using var client = ambiente.Api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        using (var resposta = await TransferirAsync(client, pf, pj.Documento, 0.10m))
        {
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(pf) == 1));
        var eventoId = (await IdsEmOrdemDeGravacaoAsync(pf)).Single();
        var duplicadosAntes = worker.Somar("banco.eventos.processados", "status", "duplicado");

        // Como uma queda entre a confirmação do broker e o commit: o evento volta a ficar pendente.
        await using (var contexto = ambiente.Api.CriarContexto())
        {
            await contexto.Database.ExecuteSqlAsync($"UPDATE outbox_eventos SET publicado_em = NULL WHERE id = {Guid.Parse(eventoId)}", Ct);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() => worker.Somar("banco.eventos.processados", "status", "duplicado") > duplicadosAntes),
            "A republicação deveria chegar ao consumidor e ser descartada como duplicada.");
        Assert.Equal(1, await ContarProcessadosAsync(pf));
        Assert.Equal(2, worker.TextosDosLogs().Count(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("Notificação de transferência", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task FilaApagada_ERecriadaEOEventoEntregue()
    {
        var worker = ambiente.Worker;
        worker.Iniciar();
        using var client = ambiente.Api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        using (var saude = worker.CreateClient())
        {
            Assert.True(await ProntoAsync(saude, HttpStatusCode.OK));
        }

        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await canal.QueueDeleteAsync("notificacoes.transferencias.fila", cancellationToken: Ct);
        }

        using (var resposta = await TransferirAsync(client, pf, pj.Documento, 0.10m))
        {
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(pf) == 1, TimeSpan.FromSeconds(30)),
            "Depois de a fila ser apagada, a topologia deveria ser declarada de novo e o evento entregue.");
    }

    private async Task PrepararEspiaAsync()
    {
        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await canal.ExchangeDeclareAsync("banco.eventos.topic", ExchangeType.Topic, durable: true, cancellationToken: Ct);
            await canal.QueueDeclareAsync(FilaEspia, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
            await canal.QueueBindAsync(FilaEspia, "banco.eventos.topic", "transferencia.concluida", cancellationToken: Ct);
            await canal.QueuePurgeAsync(FilaEspia, Ct);
        }
    }

    private async Task<List<string>> LerEspiaAsync()
    {
        var ids = new List<string>();
        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            while (await canal.BasicGetAsync(FilaEspia, autoAck: true, Ct) is { } mensagem)
            {
                Assert.True(mensagem.BasicProperties.Persistent);
                ids.Add(mensagem.BasicProperties.MessageId!);
            }
        }

        return ids;
    }

    private async Task<List<string>> IdsEmOrdemDeGravacaoAsync(ContaTeste pagador)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        var eventos = await contexto.EventosOutbox.OrderBy(e => e.OcorridoEm).ThenBy(e => e.Id).Select(e => new { e.Id, e.Payload }).ToListAsync(Ct);
        return [.. eventos.Where(e => e.Payload.Contains(pagador.UsuarioId.ToString(), StringComparison.Ordinal)).Select(e => e.Id.Valor.ToString())];
    }

    private async Task<int> ContarProcessadosAsync(ContaTeste pagador)
    {
        var ids = (await IdsEmOrdemDeGravacaoAsync(pagador)).Select(id => EventoId.De(Guid.Parse(id))).ToList();
        await using var contexto = ambiente.Api.CriarContexto();
        return await contexto.EventosProcessados.CountAsync(e => ids.Contains(e.EventoId), Ct);
    }

    private static Task<bool> ProntoAsync(HttpClient saude, HttpStatusCode esperado) =>
        BankWorkerFactory.AguardarAsync(async () =>
        {
            using var resposta = await saude.GetAsync(new Uri("/health/pronto", UriKind.Relative), Ct);
            return resposta.StatusCode == esperado;
        }, TimeSpan.FromSeconds(30));
}
