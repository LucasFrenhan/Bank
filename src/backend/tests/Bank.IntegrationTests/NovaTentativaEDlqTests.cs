using System.Collections.Concurrent;
using System.Text;
using Bank.Application.Abstracoes;
using Bank.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Bank.IntegrationTests;

/// <summary>Notificador que falha nas N primeiras chamadas de cada evento (ou sempre).</summary>
public sealed class NotificadorControlavel : INotificadorTransferencia
{
    private readonly ConcurrentDictionary<Guid, int> _falhasRestantes = new();

    public ConcurrentQueue<Guid> Notificados { get; } = new();

    public void Falhar(Guid eventoId, int vezes) => _falhasRestantes[eventoId] = vezes;

    public void FalharSempre(Guid eventoId) => _falhasRestantes[eventoId] = int.MaxValue;

    public void Normalizar(Guid eventoId) => _falhasRestantes.TryRemove(eventoId, out _);

    public Task NotificarAsync(NotificacaoTransferencia notificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacao);
        var id = notificacao.EventoId.Valor;
        if (_falhasRestantes.TryGetValue(id, out var restantes) && restantes > 0)
        {
            _falhasRestantes[id] = restantes == int.MaxValue ? restantes : restantes - 1;
            throw new InvalidOperationException("Serviço de notificação indisponível (dublê).");
        }

        Notificados.Enqueue(id);
        return Task.CompletedTask;
    }
}

/// <summary>Ambiente com o efeito do consumo trocado pelo <see cref="NotificadorControlavel"/> (T051).</summary>
public class AmbienteNotificadorControlavel : AmbienteMensageria
{
    public NotificadorControlavel Notificador { get; } = new();

    protected override void ConfigurarServicosDoWorker(IServiceCollection services) =>
        services.AddSingleton<INotificadorTransferencia>(Notificador);
}

/// <summary>Ambiente para a rede de segurança (filas de espera apagadas: a republicação volta sem rota).</summary>
public sealed class AmbienteSemFilasDeEspera : AmbienteNotificadorControlavel;

/// <summary>
/// Novas tentativas pelas filas de espera e DLQ com diagnóstico (specs/010-mensageria-rabbitmq,
/// US4: FR-018, FR-018a–c; SC-004, SC-008). Intervalos de teste: 100, 200 e 400 ms.
/// </summary>
public class NovaTentativaEDlqTests(AmbienteNotificadorControlavel ambiente) : IClassFixture<AmbienteNotificadorControlavel>
{
    internal const string Dlq = "notificacoes.transferencias.dlq";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FalhaPassageira_ProcessadaNaTerceiraExecucao_SemSegurarAsOutras()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var lenta = Guid.CreateVersion7();
        var rapida = Guid.CreateVersion7();
        ambiente.Notificador.Falhar(lenta, 2);
        var novasTentativasAntes = Contar(worker, "nova_tentativa");
        // Usuários criados antes: as duas publicações precisam sair seguidas, dentro da 1ª espera (100 ms).
        var usuarios = await UsuariosDeTeste.CriarParAsync(ambiente.Api);

        await PublicarAsync(lenta, usuarios);
        await PublicarAsync(rapida, usuarios);

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ProcessadoEmAsync(lenta) is not null));
        var (emLenta, emRapida) = (await ProcessadoEmAsync(lenta), await ProcessadoEmAsync(rapida));
        Assert.True(emRapida < emLenta, "A mensagem publicada depois deveria ser processada enquanto a outra esperava.");
        Assert.Equal(1, ambiente.Notificador.Notificados.Count(id => id == lenta));
        Assert.Equal(2, Contar(worker, "nova_tentativa") - novasTentativasAntes);
        Assert.Equal(2, worker.TextosDosLogs().Count(t => t.Contains($"EventoId={lenta}", StringComparison.Ordinal) && t.Contains("nova tentativa", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task FalhaPermanente_DlqApos4Execucoes_ReprocessamentoRecomeca()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        ambiente.Notificador.FalharSempre(eventoId);

        await PublicarAsync(eventoId);

        var naDlq = await AguardarNaDlqAsync(eventoId);
        Assert.Equal(4, Inteiro(naDlq, "x-tentativas"));
        Assert.Equal("tentativas-esgotadas", Texto(naDlq, "x-motivo"));
        Assert.Equal("InvalidOperationException: Serviço de notificação indisponível (dublê).", Texto(naDlq, "x-ultimo-erro"));
        Assert.True(DateTimeOffset.TryParse(Texto(naDlq, "x-falhou-em"), System.Globalization.CultureInfo.InvariantCulture, out _));
        Assert.Null(NovaTentativaEDlqTests.Header(naDlq, "x-reprocessamentos"));
        Assert.Equal(("transferencia.concluida", 1), (naDlq.BasicProperties.Type, Inteiro(naDlq, "versao")));
        Assert.Null(await ProcessadoEmAsync(eventoId));
        // A cópia é confirmada na DLQ antes do ACK e da métrica: aguarda o registro.
        Assert.True(await BankWorkerFactory.AguardarAsync(() => TemFalhaDlq(worker, "tentativas-esgotadas")));

        // Reprocessamento manual com a causa corrigida: processada uma vez.
        ambiente.Notificador.Normalizar(eventoId);
        await MoverParaAFilaPrincipalAsync(naDlq);
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ProcessadoEmAsync(eventoId) is not null));
        Assert.True(await BankWorkerFactory.AguardarAsync(() => worker.TextosDosLogs().Any(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("Reprocessamentos=1", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task Reprocessada_QueContinuaFalhando_VoltaADlqSemLoop()
    {
        await ambiente.Worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        ambiente.Notificador.FalharSempre(eventoId);
        await PublicarAsync(eventoId);
        var primeira = await AguardarNaDlqAsync(eventoId);

        await MoverParaAFilaPrincipalAsync(primeira);

        var segunda = await AguardarNaDlqAsync(eventoId);
        Assert.Equal((4, 1), (Inteiro(segunda, "x-tentativas"), Inteiro(segunda, "x-reprocessamentos")));
        Assert.Equal(0, ambiente.Notificador.Notificados.Count(id => id == eventoId));
    }

    [Fact]
    public async Task MensagemInvalida_DiretoParaADlq()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var messageId = Guid.CreateVersion7();

        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, "{\"eventoId\": \"x\"}", messageId.ToString(), cancellationToken: Ct);
        }

        var naDlq = await AguardarNaDlqAsync(messageId);
        Assert.Equal((1, "mensagem-invalida"), (Inteiro(naDlq, "x-tentativas"), Texto(naDlq, "x-motivo")));
        Assert.StartsWith("MensagemInvalida:", Texto(naDlq, "x-ultimo-erro"), StringComparison.Ordinal);
        Assert.True(await BankWorkerFactory.AguardarAsync(() => TemFalhaDlq(worker, "mensagem-invalida")));
    }

    private static bool TemFalhaDlq(BankWorkerFactory worker, string motivo) =>
        worker.Medicoes.Any(m => m.Nome == "banco.eventos.processados" && m.Tags["status"] == "falha_dlq" && m.Tags.GetValueOrDefault("motivo") == motivo);

    private async Task PublicarAsync(Guid eventoId, (Guid Pagador, Guid Recebedor)? usuarios = null)
    {
        var (pagador, recebedor) = usuarios ?? await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, ConsumoIdempotenteTests.Payload(eventoId, pagador, recebedor), eventoId.ToString(), cancellationToken: Ct);
        }
    }

    /// <summary>Como o "Move messages" da interface de gestão: corpo, propriedades e headers intactos.</summary>
    private async Task MoverParaAFilaPrincipalAsync(BasicGetResult mensagem)
    {
        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            var propriedades = new BasicProperties(mensagem.BasicProperties);
            await canal.BasicPublishAsync(string.Empty, "notificacoes.transferencias.fila", mandatory: true, propriedades, mensagem.Body, Ct);
        }
    }

    internal static async Task<BasicGetResult?> LerDaDlqAsync(BankWorkerFactory worker, Guid messageId)
    {
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            var devolver = new List<ulong>();
            BasicGetResult? encontrada = null;
            while (await canal.BasicGetAsync(Dlq, autoAck: false, Ct) is { } mensagem)
            {
                if (mensagem.BasicProperties.MessageId == messageId.ToString())
                {
                    encontrada = new BasicGetResult(mensagem.DeliveryTag, mensagem.Redelivered, mensagem.Exchange, mensagem.RoutingKey,
                        mensagem.MessageCount, mensagem.BasicProperties, mensagem.Body.ToArray());
                    await canal.BasicAckAsync(mensagem.DeliveryTag, multiple: false, Ct);
                    break;
                }

                devolver.Add(mensagem.DeliveryTag);
            }

            foreach (var tag in devolver)
            {
                await canal.BasicNackAsync(tag, multiple: false, requeue: true, Ct);
            }

            return encontrada;
        }
    }

    private async Task<BasicGetResult> AguardarNaDlqAsync(Guid messageId)
    {
        BasicGetResult? encontrada = null;
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => (encontrada = await LerDaDlqAsync(ambiente.Worker, messageId)) is not null),
            $"A mensagem {messageId} não chegou à DLQ.");
        return encontrada!;
    }

    private async Task<DateTimeOffset?> ProcessadoEmAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        return await contexto.EventosProcessados.Where(e => e.EventoId == EventoId.De(eventoId)).Select(e => (DateTimeOffset?)e.ProcessadoEm).SingleOrDefaultAsync(Ct);
    }

    private static int Contar(BankWorkerFactory worker, string status) =>
        worker.Medicoes.Count(m => m.Nome == "banco.eventos.processados" && m.Tags["status"] == status);

    internal static object? Header(BasicGetResult mensagem, string chave) =>
        mensagem.BasicProperties.Headers is { } headers && headers.TryGetValue(chave, out var valor) ? valor : null;

    internal static string? Texto(BasicGetResult mensagem, string chave) =>
        Header(mensagem, chave) switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            var outro => outro?.ToString(),
        };

    internal static int? Inteiro(BasicGetResult mensagem, string chave) =>
        Header(mensagem, chave) switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes), System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
}

/// <summary>
/// Rede de segurança (FR-018): se a cópia não puder ser republicada (sem rota), a original recebe
/// NACK sem requeue e chega à DLQ pela dead letter exchange da fila, só com <c>x-death</c>.
/// </summary>
public class RedeDeSegurancaDlqTests(AmbienteSemFilasDeEspera ambiente) : IClassFixture<AmbienteSemFilasDeEspera>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RepublicacaoSemConfirmacao_NackLevaADlqPelaDeadLetter()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();

        var eventoId = Guid.CreateVersion7();
        ambiente.Notificador.FalharSempre(eventoId);
        var (pagador, recebedor) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            // Sem a fila de espera, a cópia volta sem rota (mandatory) e não é confirmada.
            await canal.QueueDeleteAsync("notificacoes.transferencias.espera.100", cancellationToken: Ct);
            await BankWorkerFactory.PublicarAsync(canal, ConsumoIdempotenteTests.Payload(eventoId, pagador, recebedor), eventoId.ToString(), cancellationToken: Ct);
        }

        BasicGetResult? naDlq = null;
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => (naDlq = await NovaTentativaEDlqTests.LerDaDlqAsync(worker, eventoId)) is not null));
        Assert.NotNull(NovaTentativaEDlqTests.Header(naDlq!, "x-death"));
        Assert.Null(NovaTentativaEDlqTests.Texto(naDlq!, "x-motivo"));
        // O NACK precede o log: a mensagem pode chegar à DLQ antes de o log ser escrito.
        Assert.True(await BankWorkerFactory.AguardarAsync(() => worker.TextosDosLogs().Any(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("sem confirmação", StringComparison.Ordinal))),
            "O log de republicação sem confirmação não foi escrito.");
    }
}
