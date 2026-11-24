using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Bank.Application.Abstracoes;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Notificações de <c>cobranca.paga</c> (specs/014-cartao-credito-cobrancas, US7; FR-021, FR-035).</summary>
public class NotificacoesCobrancaTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CobrancaPaga_GeraDuasNotificacoes_NoMesmoTrace_SemDadosDoCartao()
    {
        await ambiente.Worker.IniciarProntoAsync();
        using var client = ambiente.Api.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await EmitirCartaoAsync(client, await CriarPfAsync(client));
        var cobranca = await EmitirAsync(client, pj, 1299.90m);
        using (var pagamento = await PagarAsync(client, cartao, cobranca))
        {
            Assert.Equal(HttpStatusCode.OK, pagamento.StatusCode);
        }

        await using var contexto = ambiente.Api.CriarContexto();
        var evento = contexto.EventosOutbox.AsEnumerable().Single(e => e.Tipo == "cobranca.paga" && e.Payload.Contains(cobranca.ToString(), StringComparison.Ordinal));
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await contexto.NotificacoesAuditoria.CountAsync(n => n.EventoId == evento.Id, Ct) == 2), "As notificações não foram gravadas.");

        var notificacoes = await contexto.NotificacoesAuditoria.AsNoTracking().Where(n => n.EventoId == evento.Id).ToListAsync(Ct);
        var comprovante = Assert.Single(notificacoes, n => n.Tipo == TipoNotificacao.ComprovanteCobranca);
        Assert.Equal((cartao.Conta.UsuarioId, "Você pagou R$ 1.299,90 para Empresa Exemplo Ltda no cartão de crédito."), (comprovante.UsuarioDestinoId.Valor, comprovante.Mensagem));
        var aviso = Assert.Single(notificacoes, n => n.Tipo == TipoNotificacao.CobrancaPaga);
        Assert.Equal((pj.UsuarioId, "A sua cobrança de R$ 1.299,90 foi paga."), (aviso.UsuarioDestinoId.Valor, aviso.Mensagem));
        Assert.All(notificacoes, n => Assert.DoesNotContain(cartao.UltimosDigitos, n.Mensagem, StringComparison.Ordinal));

        var id = evento.Id.Valor.ToString();
        Assert.True(await BankWorkerFactory.AguardarAsync(() => SpansMensageria.DoEvento(ambiente.Worker, "notificacoes.cobrancas.fila process", id) is not null));
        var consumo = SpansMensageria.DoEvento(ambiente.Worker, "notificacoes.cobrancas.fila process", id)!;
        Assert.Equal(evento.TraceParent!.Split('-')[1], consumo.TraceId.ToHexString());
        Assert.Contains(ambiente.Worker.Medicoes, m => m.Nome == "bank.notificacoes.enviadas" && m.Tags["papel"] == "lojista");
    }

    [Fact]
    public async Task EventoReentregue_NadaNovo()
    {
        await ambiente.Worker.IniciarProntoAsync();
        var (pagador, lojista) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        var eventoId = Guid.CreateVersion7();

        await PublicarAsync(ambiente.Worker, eventoId, pagador, lojista);
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarAsync(ambiente.Api, eventoId) == 2));
        await PublicarAsync(ambiente.Worker, eventoId, pagador, lojista);

        Assert.True(await BankWorkerFactory.AguardarAsync(() => ambiente.Worker.Medicoes.Any(m =>
            m.Nome == "banco.eventos.processados" && m.Tags["tipo_evento"] == "cobranca.paga" && m.Tags["status"] == "duplicado")));
        Assert.Equal(2, await ContarAsync(ambiente.Api, eventoId));
    }

    [Fact]
    public async Task MensagemInvalida_VaiDiretoParaADlq()
    {
        await ambiente.Worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();

        await PublicarCorpoAsync(ambiente.Worker, eventoId, $$"""{ "eventoId": "{{eventoId}}", "versao": 1 }""");

        Assert.Equal("mensagem-invalida", await AguardarMotivoNaDlqAsync(ambiente.Worker, eventoId));
        Assert.Equal(0, await ContarAsync(ambiente.Api, eventoId));
    }

    internal static async Task<int> ContarAsync(BankApiFactory api, Guid eventoId)
    {
        await using var contexto = api.CriarContexto();
        return await contexto.NotificacoesAuditoria.CountAsync(n => n.EventoId == EventoId.De(eventoId), Ct);
    }

    internal static Task PublicarAsync(BankWorkerFactory worker, Guid eventoId, Guid pagador, Guid lojista) =>
        PublicarCorpoAsync(worker, eventoId, string.Create(CultureInfo.InvariantCulture, $$"""
            { "eventoId": "{{eventoId}}", "tipoEvento": "CobrancaPaga", "versao": 1, "cobrancaId": "{{Guid.CreateVersion7()}}", "lojistaUsuarioId": "{{lojista}}",
              "pagadorUsuarioId": "{{pagador}}", "valor": 10.00, "ultimosDigitosCartao": "7899", "ocorridoEm": "2026-11-24T12:00:00+00:00", "lojistaNome": "Loja" }
            """));

    internal static async Task<string?> AguardarMotivoNaDlqAsync(BankWorkerFactory worker, Guid eventoId)
    {
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            string? motivo = null;
            Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
            {
                var mensagem = await canal.BasicGetAsync("notificacoes.cobrancas.dlq", autoAck: true, Ct);
                if (mensagem?.BasicProperties.MessageId != eventoId.ToString())
                {
                    return false;
                }

                motivo = Encoding.UTF8.GetString((byte[])mensagem.BasicProperties.Headers!["x-motivo"]!);
                return true;
            }), "A mensagem não chegou à DLQ.");
            return motivo;
        }
    }

    private static async Task PublicarCorpoAsync(BankWorkerFactory worker, Guid eventoId, string corpo)
    {
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, corpo, eventoId.ToString(), tipo: "cobranca.paga", routingKey: "cobranca.paga", cancellationToken: Ct);
        }
    }
}

/// <summary>Notificador de cobrança que falha nas N primeiras chamadas (ou sempre).</summary>
public sealed class NotificadorCobrancaControlavel : INotificadorCobranca
{
    public int FalhasRestantes { get; set; }

    public int Chamadas { get; private set; }

    public Task NotificarAsync(NotificacaoCobranca notificacao, CancellationToken cancellationToken)
    {
        Chamadas++;
        if (FalhasRestantes > 0)
        {
            FalhasRestantes--;
            throw new InvalidOperationException("Serviço de notificação indisponível (dublê).");
        }

        return Task.CompletedTask;
    }
}

public sealed class AmbienteNotificadorCobranca : AmbienteMensageria
{
    public NotificadorCobrancaControlavel Notificador { get; } = new();

    protected override void ConfigurarServicosDoWorker(IServiceCollection services) =>
        services.AddSingleton<INotificadorCobranca>(Notificador);
}

/// <summary>Novas tentativas pelas filas de espera e DLQ de cobranças (specs/014-cartao-credito-cobrancas, US7, cenário 3).</summary>
public class NovaTentativaCobrancaTests(AmbienteNotificadorCobranca ambiente) : IClassFixture<AmbienteNotificadorCobranca>
{
    [Theory]
    [InlineData(2, 3, false)]
    [InlineData(int.MaxValue, 4, true)]
    public async Task Falha_PassaPelaEspera_EEsgotadaVaiParaADlq(int falhas, int execucoes, bool dlq)
    {
        await ambiente.Worker.IniciarProntoAsync();
        var antes = ambiente.Notificador.Chamadas;
        ambiente.Notificador.FalhasRestantes = falhas;
        var eventoId = Guid.CreateVersion7();

        await NotificacoesCobrancaTests.PublicarAsync(ambiente.Worker, eventoId, Guid.CreateVersion7(), Guid.CreateVersion7());

        if (dlq)
        {
            Assert.Equal("tentativas-esgotadas", await NotificacoesCobrancaTests.AguardarMotivoNaDlqAsync(ambiente.Worker, eventoId));
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() => ambiente.Notificador.Chamadas - antes == execucoes));
        ambiente.Notificador.FalhasRestantes = 0;
    }
}
