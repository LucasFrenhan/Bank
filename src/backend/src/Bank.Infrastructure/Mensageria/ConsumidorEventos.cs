using System.Data;
using System.Diagnostics;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Base dos consumidores (specs/010-mensageria-rabbitmq, FR-015–FR-018c, FR-021, research R7):
/// sessão com canal de consumo (ACK manual, prefetch, uma entrega por vez) e canal de publicação
/// com confirms; para cada entrega, lê o contrato, registra <c>eventos_processados</c> e executa o
/// efeito na mesma transação, e só então dá ACK. Falhas vão para a fila de espera ou para a DLQ
/// conforme a <see cref="PoliticaNovaTentativa"/>; se a republicação não for confirmada, NACK sem
/// requeue (a dead letter exchange da fila leva a mensagem à DLQ).
/// </summary>
internal abstract partial class ConsumidorEventos<TMensagem>(
    IServiceScopeFactory scopeFactory,
    ConexaoRabbitMq conexao,
    IOptions<MensageriaOptions> opcoes,
    TelemetriaMensageria telemetria,
    EstadoMensageria estado,
    TimeProvider relogio,
    ILogger logger) : BackgroundService
    where TMensagem : class
{
    public const string ArquivoRegistro = "Consultas/RegistrarEventoProcessado.sql";

    protected abstract DescritorFila Fila { get; }

    /// <summary>Tipo do evento nas métricas (ex.: <c>transferencia.concluida</c>).</summary>
    protected abstract string TipoEvento { get; }

    protected abstract LeituraMensagem<TMensagem> Ler(ReadOnlyMemory<byte> corpo, IReadOnlyBasicProperties propriedades);

    /// <summary>Efeito do consumo, dentro da transação do registro de processado.</summary>
    protected abstract Task ProcessarAsync(IServiceProvider servicos, TMensagem mensagem, CancellationToken cancellationToken);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        estado.Registrar(Fila.NomeConsumidor);
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SessaoMensageria.ExecutarAsync(Fila.NomeConsumidor, SessaoAsync, logger, stoppingToken);

    private async Task SessaoAsync(Action marcarEstabelecida, CancellationToken cancellationToken)
    {
        var configuracao = opcoes.Value;
        var conexaoAberta = await conexao.ObterAsync(cancellationToken);
        await using var canalConsumo = await conexaoAberta.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false, consumerDispatchConcurrency: 1),
            cancellationToken);
        await using var canalPublicacao = await conexaoAberta.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        var perda = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        canalConsumo.ChannelShutdownAsync += (_, argumentos) => Perder(perda, $"canal de consumo fechado: {argumentos.ReplyCode} {argumentos.ReplyText}");
        canalPublicacao.ChannelShutdownAsync += (_, argumentos) => Perder(perda, $"canal de publicação fechado: {argumentos.ReplyCode} {argumentos.ReplyText}");

        await TopologiaMensageria.DeclararAsync(canalPublicacao, configuracao.IntervalosNovaTentativaMs, cancellationToken);
        await canalConsumo.BasicQosAsync(0, configuracao.Prefetch, global: false, cancellationToken);

        var publicador = new PublicadorConfirmado(canalPublicacao, TimeSpan.FromMilliseconds(configuracao.TimeoutConfirmacaoMs));
        var consumidor = new AsyncEventingBasicConsumer(canalConsumo);
        consumidor.ReceivedAsync += (_, entrega) => TratarAsync(entrega, canalConsumo, publicador, cancellationToken);
        consumidor.UnregisteredAsync += (_, _) => Perder(perda, "inscrição cancelada pelo broker");
        await canalConsumo.BasicConsumeAsync(Fila.Fila, autoAck: false, consumidor, cancellationToken);

        estado.MarcarInscrito(Fila.NomeConsumidor, inscrito: true);
        marcarEstabelecida();
        try
        {
            var motivo = await perda.Task.WaitAsync(cancellationToken);
            throw new InvalidOperationException(motivo);
        }
        finally
        {
            estado.MarcarInscrito(Fila.NomeConsumidor, inscrito: false);
        }
    }

    private static Task Perder(TaskCompletionSource<string> perda, string motivo)
    {
        perda.TrySetResult(motivo);
        return Task.CompletedTask;
    }

    /// <summary>Uma entrega, do contrato ao ACK (research R7).</summary>
    private async Task TratarAsync(BasicDeliverEventArgs entrega, IChannel canalConsumo, IPublicadorConfirmado publicador, CancellationToken cancellationToken)
    {
        var propriedades = entrega.BasicProperties;
        var (execucao, reprocessamentos) = PoliticaNovaTentativa.Contar(propriedades.Headers);

        var pai = CabecalhosMensagem.ExtrairContexto(propriedades.Headers);
        using var atividade = TelemetriaMensageria.Fonte.StartActivity($"{Fila.Fila} process", ActivityKind.Consumer, pai);
        atividade?.SetTag("messaging.system", "rabbitmq");
        atividade?.SetTag("messaging.operation.type", "process");
        atividade?.SetTag("messaging.destination.name", Fila.Fila);
        atividade?.SetTag("messaging.message.id", propriedades.MessageId);
        atividade?.SetTag("bank.mensageria.tentativa", execucao);

        try
        {
            var leitura = Ler(entrega.Body, propriedades);
            if (!leitura.Valida)
            {
                await EncaminharFalhaAsync(entrega, canalConsumo, publicador, leitura.EventoId, erro: null, leitura.Erro, atividade, cancellationToken);
                return;
            }

            var eventoId = leitura.EventoId!.Value;
            Exception? falha = null;
            var duplicado = false;
            try
            {
                duplicado = !await ProcessarUmaVezAsync(eventoId, leitura.Conteudo!, cancellationToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                falha = excecao;
            }

            if (falha is not null)
            {
                await EncaminharFalhaAsync(entrega, canalConsumo, publicador, eventoId, falha, null, atividade, cancellationToken);
                return;
            }

            await canalConsumo.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken);
            if (duplicado)
            {
                telemetria.RegistrarProcessado(TipoEvento, TelemetriaMensageria.StatusDuplicado);
                atividade?.SetTag("bank.mensageria.resultado", TelemetriaMensageria.StatusDuplicado);
                LogDuplicado(logger, eventoId, Fila.NomeConsumidor);
                return;
            }

            telemetria.RegistrarProcessado(TipoEvento, TelemetriaMensageria.StatusSucesso);
            atividade?.SetTag("bank.mensageria.resultado", TelemetriaMensageria.StatusSucesso);
            if (reprocessamentos > 0)
            {
                LogReprocessado(logger, eventoId, reprocessamentos);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Encerrando: sem ACK, o broker devolve a mensagem à fila quando o canal fechar.
        }
    }

    /// <summary>Registra o processado e executa o efeito numa transação; falso = já processado.</summary>
    private async Task<bool> ProcessarUmaVezAsync(Guid eventoId, TMensagem mensagem, CancellationToken cancellationToken)
    {
        await using var escopo = scopeFactory.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<BankDbContext>();
        await using var transacao = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var inseridas = await contexto.Database.ExecuteSqlRawAsync(
            SqlEmbutido.Ler(ArquivoRegistro),
            [
                new NpgsqlParameter("evento_id", eventoId),
                new NpgsqlParameter("nome_consumidor", Fila.NomeConsumidor),
                new NpgsqlParameter("processado_em", relogio.GetUtcNow()),
            ],
            cancellationToken);

        if (inseridas == 0)
        {
            await transacao.CommitAsync(cancellationToken);
            return false;
        }

        await ProcessarAsync(escopo.ServiceProvider, mensagem, cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return true;
    }

    private async Task EncaminharFalhaAsync(
        BasicDeliverEventArgs entrega,
        IChannel canalConsumo,
        IPublicadorConfirmado publicador,
        Guid? eventoId,
        Exception? erro,
        string? erroLeitura,
        Activity? atividade,
        CancellationToken cancellationToken)
    {
        var destino = PoliticaNovaTentativa.Decidir(
            Fila, opcoes.Value.IntervalosNovaTentativaMs, entrega.BasicProperties.Headers, erro, erroLeitura, relogio.GetUtcNow());
        var propriedades = CabecalhosMensagem.Copiar(entrega.BasicProperties, destino.Headers);
        var publicado = await publicador.PublicarAsync(destino.Exchange, destino.RoutingKey, propriedades, entrega.Body, cancellationToken);

        var status = destino.ParaDlq ? TelemetriaMensageria.StatusFalhaDlq : TelemetriaMensageria.StatusNovaTentativa;
        var motivo = destino.Motivo ?? (publicado == ResultadoPublicacao.Confirmado ? null : PoliticaNovaTentativa.MotivoTentativasEsgotadas);
        atividade?.SetStatus(ActivityStatusCode.Error, destino.Motivo ?? "nova tentativa");
        atividade?.SetTag("bank.mensageria.resultado", publicado == ResultadoPublicacao.Confirmado ? status : TelemetriaMensageria.StatusFalhaDlq);

        if (publicado != ResultadoPublicacao.Confirmado)
        {
            // Rede de segurança (FR-018): a dead letter exchange da fila leva a original à DLQ.
            await canalConsumo.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            telemetria.RegistrarProcessado(TipoEvento, TelemetriaMensageria.StatusFalhaDlq, motivo);
            LogRepublicacaoFalhou(logger, eventoId, DespachanteOutbox.Motivo(publicado), destino.RoutingKey);
            return;
        }

        await canalConsumo.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken);
        var tipoErro = erro?.GetType().Name ?? "MensagemInvalida";
        if (destino.ParaDlq)
        {
            telemetria.RegistrarProcessado(TipoEvento, status, destino.Motivo);
            LogEnviadoParaDlq(logger, eventoId, destino.Motivo!, tipoErro, destino.Execucao, destino.Reprocessamentos);
        }
        else
        {
            telemetria.RegistrarProcessado(TipoEvento, status);
            LogNovaTentativa(logger, eventoId, destino.Execucao, destino.IntervaloMs!.Value, tipoErro);
        }
    }

    [LoggerMessage(EventId = 5003, EventName = "NovaTentativa", Level = LogLevel.Warning,
        Message = "Evento {EventoId} falhou na execução {Execucao} ({TipoErro}); nova tentativa em {IntervaloMs} ms")]
    private static partial void LogNovaTentativa(ILogger logger, Guid? eventoId, int execucao, int intervaloMs, string tipoErro);

    [LoggerMessage(EventId = 5004, EventName = "EnviadoParaDlq", Level = LogLevel.Error,
        Message = "Evento {EventoId} enviado para a DLQ: {Motivo} ({TipoErro}) após {Execucoes} execução(ões), {Reprocessamentos} reprocessamento(s)")]
    private static partial void LogEnviadoParaDlq(ILogger logger, Guid? eventoId, string motivo, string tipoErro, int execucoes, int reprocessamentos);

    [LoggerMessage(EventId = 5005, EventName = "EventoDuplicado", Level = LogLevel.Information,
        Message = "Evento {EventoId} já processado por {Consumidor}; descartado")]
    private static partial void LogDuplicado(ILogger logger, Guid eventoId, string consumidor);

    [LoggerMessage(EventId = 5006, EventName = "RepublicacaoFalhou", Level = LogLevel.Error,
        Message = "Republicação do evento {EventoId} em {Destino} sem confirmação ({Motivo}); NACK para a dead letter exchange")]
    private static partial void LogRepublicacaoFalhou(ILogger logger, Guid? eventoId, string motivo, string destino);

    [LoggerMessage(EventId = 5007, EventName = "EventoReprocessado", Level = LogLevel.Information,
        Message = "Evento {EventoId} reprocessado da DLQ com sucesso (x-reprocessamentos = {Reprocessamentos})")]
    private static partial void LogReprocessado(ILogger logger, Guid eventoId, int reprocessamentos);
}
