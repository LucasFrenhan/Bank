using System.Diagnostics.Metrics;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas das notificações (specs/010-mensageria-rabbitmq, research R10). As tags nunca carregam
/// dados pessoais nem identificadores.
/// </summary>
public sealed class MetricasNotificacoes
{
    public const string NomeMeter = "Bank.Notificacoes";
    public const string NomeEnviadas = "bank.notificacoes.enviadas";

    private readonly Counter<long> _enviadas;

    public MetricasNotificacoes(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _enviadas = meter.CreateCounter<long>(NomeEnviadas, "{notificacao}", "Notificações de transferência enviadas, por papel do destinatário.");
    }

    public void RegistrarEnviada(string papel) => _enviadas.Add(1, new KeyValuePair<string, object?>("papel", papel));
}
