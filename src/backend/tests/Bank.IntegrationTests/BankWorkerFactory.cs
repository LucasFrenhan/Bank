using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Bank.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace Bank.IntegrationTests;

/// <summary>
/// Hospeda o Worker em memória no ambiente <c>Testing</c> (intervalos curtos, sem OTLP) contra o
/// banco de uma <see cref="BankApiFactory"/> (já migrado pela API) e um vhost exclusivo do RabbitMQ
/// (specs/010-mensageria-rabbitmq, research R12). Captura spans, logs e cada medição dos meters
/// do Worker.
/// </summary>
public sealed class BankWorkerFactory(string connectionString, BrokerDeTeste broker, Action<IServiceCollection>? servicosDeTeste = null)
    : WebApplicationFactory<WorkerAssemblyMarker>
{
    private readonly Lock _trava = new();
    private MeterListener? _ouvinte;

    public BrokerDeTeste Broker { get; } = broker;

    public SynchronizedCollection<Activity> Spans { get; } = new();

    public SynchronizedCollection<LogRecord> Logs { get; } = new();

    public ConcurrentQueue<(string Nome, double Valor, Dictionary<string, string?> Tags)> Medicoes { get; } = new();

    /// <summary>Inicia o Worker (hosted services) e a captura de métricas.</summary>
    public void Iniciar()
    {
        _ = Server;
        lock (_trava)
        {
            if (_ouvinte is not null)
            {
                return;
            }

            var fabricaDeMeters = Services.GetRequiredService<IMeterFactory>();
            _ouvinte = new MeterListener
            {
                InstrumentPublished = (instrumento, ouvinte) =>
                {
                    if (ReferenceEquals(instrumento.Meter.Scope, fabricaDeMeters))
                    {
                        ouvinte.EnableMeasurementEvents(instrumento);
                    }
                },
            };
            _ouvinte.SetMeasurementEventCallback<long>((i, v, tags, _) => Registrar(i.Name, v, tags));
            _ouvinte.SetMeasurementEventCallback<double>((i, v, tags, _) => Registrar(i.Name, v, tags));
            _ouvinte.Start();
        }
    }

    /// <summary>Inicia o Worker e espera o consumidor inscrito (topologia declarada) — antes de publicar à mão.</summary>
    public async Task IniciarProntoAsync()
    {
        Iniciar();
        using var saude = CreateClient();
        var pronto = await AguardarAsync(async () =>
        {
            using var resposta = await saude.GetAsync(new Uri("/health/pronto", UriKind.Relative));
            return resposta.IsSuccessStatusCode;
        }, TimeSpan.FromSeconds(30));
        if (!pronto)
        {
            throw new InvalidOperationException("O Worker não ficou pronto em 30 s.");
        }
    }

    /// <summary>Quantas medições do instrumento têm a tag <paramref name="tag"/> = <paramref name="valor"/>.</summary>
    public double Somar(string instrumento, string? tag = null, string? valor = null) =>
        Medicoes.Where(m => m.Nome == instrumento && (tag is null || (m.Tags.TryGetValue(tag, out var v) && v == valor))).Sum(m => m.Valor);

    /// <summary>Textos de todos os logs exportados (mensagem formatada e atributos).</summary>
    public IReadOnlyList<string> TextosDosLogs() =>
        [.. Logs.Copiar().Select(l => string.Join(' ', [l.FormattedMessage ?? string.Empty, .. (l.Attributes ?? []).Select(a => $"{a.Key}={a.Value}")]))];

    /// <summary>Canal de teste no vhost do Worker.</summary>
    public async Task<(IConnection Conexao, IChannel Canal)> AbrirCanalAsync(CancellationToken cancellationToken)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = Broker.Host,
            Port = Broker.Porta,
            VirtualHost = Broker.VirtualHost,
            UserName = RabbitMqCompartilhado.Usuario,
            Password = RabbitMqCompartilhado.Senha,
        };
        var conexao = await fabrica.CreateConnectionAsync(cancellationToken);
        var canal = await conexao.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        return (conexao, canal);
    }

    /// <summary>Publica uma mensagem crua na exchange de eventos (como a interface de gestão faria).</summary>
    public static async Task PublicarAsync(IChannel canal, string corpo, string messageId, string? tipo = "transferencia.concluida",
        int? versao = 1, IDictionary<string, object?>? headersExtras = null, string exchange = "banco.eventos.topic",
        string routingKey = "transferencia.concluida", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canal);
        var headers = new Dictionary<string, object?>();
        if (versao is not null)
        {
            headers["versao"] = versao.Value;
        }

        foreach (var (chave, valor) in headersExtras ?? new Dictionary<string, object?>())
        {
            headers[chave] = valor;
        }

        var propriedades = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Persistent = true,
            MessageId = messageId,
            Type = tipo,
            Headers = headers,
        };
        await canal.BasicPublishAsync(exchange, routingKey, mandatory: false, propriedades, Encoding.UTF8.GetBytes(corpo), cancellationToken);
    }

    /// <summary>Espera até a condição valer ou o prazo acabar.</summary>
    public static async Task<bool> AguardarAsync(Func<Task<bool>> condicao, TimeSpan? prazo = null)
    {
        ArgumentNullException.ThrowIfNull(condicao);
        var limite = Stopwatch.StartNew();
        var total = prazo ?? TimeSpan.FromSeconds(15);
        while (limite.Elapsed < total)
        {
            if (await condicao())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return await condicao();
    }

    public static Task<bool> AguardarAsync(Func<bool> condicao, TimeSpan? prazo = null) =>
        AguardarAsync(() => Task.FromResult(condicao()), prazo);

    public override async ValueTask DisposeAsync()
    {
        _ouvinte?.Dispose();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Bank", connectionString);
        builder.UseSetting("Mensageria:Host", Broker.Host);
        builder.UseSetting("Mensageria:Porta", Broker.Porta.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Mensageria:VirtualHost", Broker.VirtualHost);
        builder.UseSetting("Mensageria:Usuario", RabbitMqCompartilhado.Usuario);
        builder.UseSetting("Mensageria:Senha", RabbitMqCompartilhado.Senha);
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(Logs));
            servicosDeTeste?.Invoke(services);
        });
    }

    private void Registrar(string nome, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        Medicoes.Enqueue((nome, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));
}

/// <summary>
/// API + Worker + vhost exclusivo, para as classes de teste da mensageria (fixture de classe).
/// A API usa a fábrica com captura de telemetria, para os testes de trace ponta a ponta.
/// </summary>
public class AmbienteMensageria : IAsyncLifetime
{
    private BankWorkerFactory? _worker;

    public TelemetriaCompletaApiFactory Api { get; } = new();

    public BankWorkerFactory Worker => _worker ?? throw new InvalidOperationException("Ambiente não inicializado.");

    /// <summary>Serviços trocados no Worker (dublês); sobrescrito pelas fixtures derivadas.</summary>
    protected virtual void ConfigurarServicosDoWorker(IServiceCollection services)
    {
    }

    /// <summary>Broker usado (sobrescrito pelos testes que precisam de um contêiner exclusivo).</summary>
    protected virtual Task<BrokerDeTeste> CriarBrokerAsync() => RabbitMqCompartilhado.CriarVirtualHostAsync();

    public virtual async ValueTask InitializeAsync()
    {
        await Api.InitializeAsync();
        _ = Api.Server; // migrations aplicadas antes de o Worker usar o banco
        var broker = await CriarBrokerAsync();
        _worker = new BankWorkerFactory(Api.ConnectionString, broker, ConfigurarServicosDoWorker);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.DisposeAsync();
        }

        await Api.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
