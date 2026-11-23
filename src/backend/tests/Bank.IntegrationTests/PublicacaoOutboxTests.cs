using System.Diagnostics;
using System.Net;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Transferência concluída até a notificação (specs/010-mensageria-rabbitmq, US1: FR-001, FR-002,
/// FR-015, FR-016; SC-001, SC-007) e saúde do Worker (FR-024, Princípio VII).
/// </summary>
public class PublicacaoOutboxTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Transferencia_EPublicadaENotificadaUmaVez_ComDuasNotificacoesRegistradas()
    {
        ambiente.Worker.Iniciar();
        using var client = ambiente.Api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);

        var relogio = Stopwatch.StartNew();
        using (var resposta = await TransferirAsync(client, pf, pj.Documento, 1.00m, descricao: "Café"))
        {
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        var tempoDeResposta = relogio.Elapsed;
        var eventoId = await EventoMaisRecenteAsync(pf.UsuarioId);

        Assert.True(await BankWorkerFactory.AguardarAsync(() => ProcessadoAsync(eventoId), TimeSpan.FromSeconds(5)),
            "O evento não foi processado em até 5 s (SC-001).");
        Assert.True(tempoDeResposta < TimeSpan.FromSeconds(1), $"A transferência levou {tempoDeResposta} (SC-007).");

        await using (var contexto = ambiente.Api.CriarContexto())
        {
            var outbox = await contexto.EventosOutbox.SingleAsync(e => e.Id == EventoId.De(eventoId), Ct);
            Assert.NotNull(outbox.PublicadoEm);
            var processado = await contexto.EventosProcessados.SingleAsync(e => e.EventoId == EventoId.De(eventoId), Ct);
            Assert.Equal("notificacoes-transferencias", processado.NomeConsumidor);
        }

        var worker = ambiente.Worker;
        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            worker.TextosDosLogs().Count(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("Notificação de transferência", StringComparison.Ordinal)) == 2));
        Assert.Equal(1, worker.Medicoes.Count(m => m.Nome == "banco.eventos.processados" && m.Tags["status"] == "sucesso" && m.Tags["tipo_evento"] == "transferencia.concluida"));
        Assert.True(worker.Somar("banco.eventos.publicados", "tipo_evento", "transferencia.concluida") >= 1);

        // Spec 011 (US1, FR-001–FR-004): comprovante para o pagador e aviso de crédito para o recebedor.
        await using (var contexto = ambiente.Api.CriarContexto())
        {
            var evento = EventoId.De(eventoId);
            var notificacoes = await contexto.NotificacoesAuditoria.Where(n => n.EventoId == evento).ToListAsync(Ct);
            var nomePf = await contexto.Usuarios.Where(u => u.Id == UsuarioId.De(pf.UsuarioId)).Select(u => u.NomeCompleto).SingleAsync(Ct);
            var nomePj = await contexto.Usuarios.Where(u => u.Id == UsuarioId.De(pj.UsuarioId)).Select(u => u.NomeCompleto).SingleAsync(Ct);
            var ocorridoEm = await contexto.EventosOutbox.Where(e => e.Id == evento).Select(e => e.OcorridoEm).SingleAsync(Ct);

            Assert.Equal(2, notificacoes.Count);
            var comprovante = Assert.Single(notificacoes, n => n.Tipo == TipoNotificacao.ComprovanteEnvio);
            var aviso = Assert.Single(notificacoes, n => n.Tipo == TipoNotificacao.AvisoCredito);
            Assert.Equal(UsuarioId.De(pf.UsuarioId), comprovante.UsuarioDestinoId);
            Assert.Equal(UsuarioId.De(pj.UsuarioId), aviso.UsuarioDestinoId);
            Assert.Equal($"Você enviou R$ 1,00 para {nomePj}.", comprovante.Mensagem);
            Assert.Equal($"Você recebeu R$ 1,00 de {nomePf}.", aviso.Mensagem);
            Assert.All(notificacoes, n =>
            {
                Assert.Equal(CanalNotificacao.Simulado, n.Canal);
                Assert.True(n.CriadoEm >= ocorridoEm, "A notificação não pode ser anterior ao evento.");
                Assert.DoesNotContain("Café", n.Mensagem, StringComparison.Ordinal); // FR-004: sem descrição
            });
        }
    }

    [Fact]
    public async Task Saude_VivoEProntoComOConsumidorInscrito()
    {
        ambiente.Worker.Iniciar();
        using var client = ambiente.Worker.CreateClient();

        using (var vivo = await client.GetAsync(new Uri("/health", UriKind.Relative), Ct))
        {
            Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            using var pronto = await client.GetAsync(new Uri("/health/pronto", UriKind.Relative), Ct);
            return pronto.StatusCode == HttpStatusCode.OK;
        }), "/health/pronto não respondeu 200 com o consumidor inscrito.");
    }

    private async Task<Guid> EventoMaisRecenteAsync(Guid pagadorUsuarioId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        var payloads = await contexto.EventosOutbox.OrderByDescending(e => e.OcorridoEm).Select(e => new { e.Id, e.Payload }).ToListAsync(Ct);
        return payloads.First(p => p.Payload.Contains(pagadorUsuarioId.ToString(), StringComparison.Ordinal)).Id.Valor;
    }

    private async Task<bool> ProcessadoAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        return await contexto.EventosProcessados.AnyAsync(e => e.EventoId == EventoId.De(eventoId), Ct);
    }
}
