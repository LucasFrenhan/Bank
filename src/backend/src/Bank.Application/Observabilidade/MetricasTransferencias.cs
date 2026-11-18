using System.Diagnostics.Metrics;
using Bank.Domain.Usuarios;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas de negócio de transferências (spec 006, FR-020 e FR-009b; research R10). As tags
/// nunca carregam documento, nome, e-mail ou identificador de usuário.
/// </summary>
public sealed class MetricasTransferencias
{
    public const string NomeMeter = "Bank.Transferencias";

    private readonly Counter<long> _concluidas;
    private readonly Counter<long> _recusadas;
    private readonly Counter<long> _replays;
    private readonly Histogram<double> _valor;
    private readonly Counter<long> _consultasDestinatario;
    private readonly Counter<long> _consultas;

    public MetricasTransferencias(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _concluidas = meter.CreateCounter<long>("bank.transferencias.concluidas", "{transferencia}", "Transferências concluídas.");
        _recusadas = meter.CreateCounter<long>("bank.transferencias.recusadas", "{transferencia}", "Transferências recusadas, por motivo.");
        _replays = meter.CreateCounter<long>("bank.transferencias.replays", "{requisicao}", "Reenvios idempotentes respondidos com o comprovante original.");
        _valor = meter.CreateHistogram<double>("bank.transferencias.valor", "BRL", "Valor das transferências concluídas.");
        _consultasDestinatario = meter.CreateCounter<long>("bank.transferencias.destinatario.consultas", "{consulta}", "Consultas de destinatário, por resultado.");
        _consultas = meter.CreateCounter<long>("bank.transferencias.consultas", "{consulta}", "Consultas de transferência por identificador, por resultado.");
    }

    public void RegistrarConcluida(TipoConta tipoDestino, decimal valor)
    {
        var tag = new KeyValuePair<string, object?>("tipo_destino", tipoDestino.ToString());
        _concluidas.Add(1, tag);
        _valor.Record((double)valor, tag);
    }

    public void RegistrarRecusa(string motivo) =>
        _recusadas.Add(1, new KeyValuePair<string, object?>("motivo", motivo));

    public void RegistrarReplay() => _replays.Add(1);

    public void RegistrarConsultaDestinatario(string resultado) =>
        _consultasDestinatario.Add(1, new KeyValuePair<string, object?>("resultado", resultado));

    /// <summary>
    /// Consulta por identificador (spec 008, FR-007): <c>encontrada</c>, <c>nao-encontrada</c> ou
    /// <c>invalida</c>. Nunca o identificador como rótulo.
    /// </summary>
    public void RegistrarConsulta(string resultado) =>
        _consultas.Add(1, new KeyValuePair<string, object?>("resultado", resultado));
}
