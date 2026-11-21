using System.Data;
using System.Diagnostics;
using System.Text;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>Resultado de uma rodada do despachante.</summary>
/// <param name="Falha">Desfecho do evento que parou o lote; nulo se todos foram confirmados.</param>
internal sealed record ResultadoLote(int Lidos, int Publicados, ResultadoPublicacao? Falha);

/// <summary>
/// Despachante do outbox — o "publicador" da spec (specs/010-mensageria-rabbitmq, FR-001–FR-005,
/// FR-020, research R4, R6). A cada rodada reserva pendentes (<c>FOR UPDATE SKIP LOCKED</c>),
/// publica em ordem com <c>mandatory</c> e confirmação do broker, marca <c>publicado_em</c> só dos
/// confirmados e para no primeiro que falha. Cada publicação abre um span Producer filho do
/// contexto guardado no outbox e injeta o próprio contexto nos headers.
/// </summary>
internal sealed partial class DespachanteOutbox(
    IServiceScopeFactory scopeFactory,
    ConexaoRabbitMq conexao,
    IOptions<MensageriaOptions> opcoes,
    TelemetriaMensageria telemetria,
    TimeProvider relogio,
    ILogger<DespachanteOutbox> logger) : BackgroundService
{
    public const string ArquivoReserva = "Consultas/ReservarEventosPendentes.sql";
    public const string NomeSpan = TopologiaMensageria.ExchangeEventos + " publish";

    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(10);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SessaoMensageria.ExecutarAsync("despachante-outbox", SessaoAsync, logger, stoppingToken);

    private async Task SessaoAsync(Action marcarEstabelecida, CancellationToken cancellationToken)
    {
        var configuracao = opcoes.Value;
        var conexaoAberta = await conexao.ObterAsync(cancellationToken);
        await using var canal = await conexaoAberta.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await TopologiaMensageria.DeclararAsync(canal, configuracao.IntervalosNovaTentativaMs, cancellationToken);
        var publicador = new PublicadorConfirmado(canal, TimeSpan.FromMilliseconds(configuracao.TimeoutConfirmacaoMs));
        marcarEstabelecida();

        var falhasSeguidas = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var resultado = await DespacharLoteAsync(publicador, cancellationToken);
            if (resultado.Falha == ResultadoPublicacao.CanalFechado || !canal.IsOpen)
            {
                return; // a sessão termina; o laço abre outra
            }

            falhasSeguidas = resultado.Falha is null ? 0 : falhasSeguidas + 1;
            var espera = EsperaAteAProximaRodada(resultado, falhasSeguidas, configuracao);
            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, cancellationToken);
            }
        }
    }

    /// <summary>Lote cheio e sem falha: segue na hora; com falha: 2^n s, teto de 10 s.</summary>
    internal static TimeSpan EsperaAteAProximaRodada(ResultadoLote resultado, int falhasSeguidas, MensageriaOptions configuracao)
    {
        if (resultado.Falha is not null)
        {
            var segundos = Math.Min(Math.Pow(2, falhasSeguidas), EsperaMaxima.TotalSeconds);
            return TimeSpan.FromSeconds(segundos);
        }

        return resultado.Lidos >= configuracao.TamanhoLoteDespacho ? TimeSpan.Zero : TimeSpan.FromMilliseconds(configuracao.IntervaloDespachoMs);
    }

    private async Task<ResultadoLote> DespacharLoteAsync(IPublicadorConfirmado publicador, CancellationToken cancellationToken)
    {
        await using var escopo = scopeFactory.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<BankDbContext>();

        await using var transacao = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var eventos = await contexto.EventosOutbox
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoReserva), new NpgsqlParameter("lote", opcoes.Value.TamanhoLoteDespacho))
            .ToListAsync(cancellationToken);

        var resultado = await PublicarLoteAsync(eventos, publicador, cancellationToken);

        await contexto.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        telemetria.AtualizarPendentes(await contexto.EventosOutbox.CountAsync(e => e.PublicadoEm == null, cancellationToken));
        return resultado;
    }

    /// <summary>Publica em ordem e marca os confirmados; para no primeiro que falha (FR-002).</summary>
    internal async Task<ResultadoLote> PublicarLoteAsync(IReadOnlyList<EventoOutbox> eventos, IPublicadorConfirmado publicador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        ArgumentNullException.ThrowIfNull(publicador);

        var publicados = 0;
        foreach (var evento in eventos)
        {
            var resultado = await PublicarAsync(evento, publicador, cancellationToken);
            if (resultado != ResultadoPublicacao.Confirmado)
            {
                return new ResultadoLote(eventos.Count, publicados, resultado);
            }

            evento.MarcarPublicado(relogio.GetUtcNow());
            publicados++;
        }

        return new ResultadoLote(eventos.Count, publicados, null);
    }

    private async Task<ResultadoPublicacao> PublicarAsync(EventoOutbox evento, IPublicadorConfirmado publicador, CancellationToken cancellationToken)
    {
        // Sem contexto gravado (eventos antigos ou sem trace), o span abre um trace novo (R6).
        var pai = ActivityContext.TryParse(evento.TraceParent, evento.TraceState, out var gravado) ? gravado : default;
        using var atividade = TelemetriaMensageria.Fonte.StartActivity(NomeSpan, ActivityKind.Producer, pai);
        atividade?.SetTag("messaging.system", "rabbitmq");
        atividade?.SetTag("messaging.operation.type", "send");
        atividade?.SetTag("messaging.destination.name", TopologiaMensageria.ExchangeEventos);
        atividade?.SetTag("messaging.rabbitmq.destination.routing_key", evento.Tipo);
        atividade?.SetTag("messaging.message.id", evento.Id.Valor.ToString());

        var propriedades = CabecalhosMensagem.ParaPublicacao(evento);
        CabecalhosMensagem.InjetarContexto(atividade?.Context ?? pai, propriedades.Headers!);

        var resultado = await publicador.PublicarAsync(
            TopologiaMensageria.ExchangeEventos, evento.Tipo, propriedades, Encoding.UTF8.GetBytes(evento.Payload), cancellationToken);

        if (resultado == ResultadoPublicacao.Confirmado)
        {
            telemetria.RegistrarPublicado(evento.Tipo);
            LogEventoPublicado(logger, evento.Id.Valor, evento.Tipo);
            return resultado;
        }

        var motivo = Motivo(resultado);
        atividade?.SetStatus(ActivityStatusCode.Error, motivo);
        telemetria.RegistrarFalhaPublicacao(evento.Tipo, motivo);
        LogFalhaPublicacao(logger, evento.Id.Valor, evento.Tipo, motivo);
        return resultado;
    }

    internal static string Motivo(ResultadoPublicacao resultado) => resultado switch
    {
        ResultadoPublicacao.SemRota => "sem-rota",
        ResultadoPublicacao.Nack => "nack",
        ResultadoPublicacao.Timeout => "timeout",
        _ => "canal",
    };

    [LoggerMessage(EventId = 5001, EventName = "EventoPublicado", Level = LogLevel.Debug,
        Message = "Evento {EventoId} ({Tipo}) publicado")]
    private static partial void LogEventoPublicado(ILogger logger, Guid eventoId, string tipo);

    [LoggerMessage(EventId = 5002, EventName = "FalhaPublicacao", Level = LogLevel.Warning,
        Message = "Falha ao publicar o evento {EventoId} ({Tipo}): {Motivo}. O evento continua pendente")]
    private static partial void LogFalhaPublicacao(ILogger logger, Guid eventoId, string tipo, string motivo);
}
