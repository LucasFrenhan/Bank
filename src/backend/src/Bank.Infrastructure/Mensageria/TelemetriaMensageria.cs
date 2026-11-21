using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Spans e métricas da mensageria (specs/010-mensageria-rabbitmq, FR-020–FR-022, research R10).
/// As tags nunca carregam identificadores de usuário ou de evento, nomes nem payload (FR-023).
/// </summary>
public sealed class TelemetriaMensageria
{
    public const string Nome = "Bank.Mensageria";

    public const string StatusSucesso = "sucesso";
    public const string StatusDuplicado = "duplicado";
    public const string StatusNovaTentativa = "nova_tentativa";
    public const string StatusFalhaDlq = "falha_dlq";

    internal static readonly ActivitySource Fonte = new(Nome);

    private readonly Counter<long> _processados;
    private readonly Counter<long> _publicados;
    private readonly Counter<long> _falhasPublicacao;
    private long _pendentes;

    public TelemetriaMensageria(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(Nome);
        _processados = meter.CreateCounter<long>("banco.eventos.processados", "{evento}", "Eventos processados pelos consumidores, por status.");
        _publicados = meter.CreateCounter<long>("banco.eventos.publicados", "{evento}", "Eventos do outbox publicados com confirmação do broker.");
        _falhasPublicacao = meter.CreateCounter<long>("banco.eventos.publicacao.falhas", "{evento}", "Falhas de publicação de eventos do outbox, por motivo.");
        meter.CreateObservableGauge("banco.outbox.pendentes", () => Interlocked.Read(ref _pendentes), "{evento}", "Eventos do outbox ainda não publicados (última leitura do despachante).");
    }

    public void RegistrarProcessado(string tipoEvento, string status, string? motivo = null)
    {
        var tags = new TagList { { "tipo_evento", tipoEvento }, { "status", status } };
        if (motivo is not null)
        {
            tags.Add("motivo", motivo);
        }

        _processados.Add(1, tags);
    }

    public void RegistrarPublicado(string tipoEvento) => _publicados.Add(1, new KeyValuePair<string, object?>("tipo_evento", tipoEvento));

    public void RegistrarFalhaPublicacao(string tipoEvento, string motivo) =>
        _falhasPublicacao.Add(1, new KeyValuePair<string, object?>("tipo_evento", tipoEvento), new KeyValuePair<string, object?>("motivo", motivo));

    public void AtualizarPendentes(long pendentes) => Interlocked.Exchange(ref _pendentes, pendentes);
}
