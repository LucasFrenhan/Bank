using System.Diagnostics.Metrics;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas de negócio de transferências (spec 006, FR-020 e FR-009b; research R10). As tags
/// nunca carregam documento, nome, e-mail ou identificador de usuário.
/// </summary>
public sealed class MetricasTransferencias
{
    public const string NomeMeter = "Bank.Transferencias";

    private const string CodigoLimiteExcedido = "limite-excedido";

    private readonly Counter<long> _concluidas;
    private readonly Counter<long> _recusadas;
    private readonly Counter<long> _replays;
    private readonly Histogram<double> _valor;
    private readonly Counter<long> _consultasDestinatario;
    private readonly Counter<long> _consultas;
    private readonly Counter<long> _consultasLimites;

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
        _consultasLimites = meter.CreateCounter<long>("bank.transferencias.limites.consultas", "{consulta}", "Consultas de limites de transferência, por resultado.");
    }

    public void RegistrarConcluida(TipoConta tipoDestino, decimal valor)
    {
        var tag = new KeyValuePair<string, object?>("tipo_destino", tipoDestino.ToString());
        _concluidas.Add(1, tag);
        _valor.Record((double)valor, tag);
    }

    public void RegistrarRecusa(string motivo) =>
        _recusadas.Add(1, new KeyValuePair<string, object?>("motivo", motivo));

    /// <summary>
    /// Recusa pelo erro: <c>limite-excedido</c> ganha a tag <c>tipo_limite</c>
    /// (specs/009-limite-transferencia, FR-015; research R7); as demais só o motivo.
    /// </summary>
    public void RegistrarRecusa(Erro erro)
    {
        ArgumentNullException.ThrowIfNull(erro);

        if (erro.Codigo == CodigoLimiteExcedido && erro.Detalhes?.GetValueOrDefault("tipoLimite") is string tipoLimite)
        {
            _recusadas.Add(
                1,
                new KeyValuePair<string, object?>("motivo", erro.Codigo),
                new KeyValuePair<string, object?>("tipo_limite", tipoLimite));
            return;
        }

        RegistrarRecusa(erro.Codigo);
    }

    public void RegistrarReplay() => _replays.Add(1);

    public void RegistrarConsultaDestinatario(string resultado) =>
        _consultasDestinatario.Add(1, new KeyValuePair<string, object?>("resultado", resultado));

    /// <summary>
    /// Consulta por identificador (spec 008, FR-007): <c>encontrada</c>, <c>nao-encontrada</c> ou
    /// <c>invalida</c>. Nunca o identificador como rótulo.
    /// </summary>
    public void RegistrarConsulta(string resultado) =>
        _consultas.Add(1, new KeyValuePair<string, object?>("resultado", resultado));

    /// <summary>
    /// Consulta de limites (specs/009-limite-transferencia, R7): <c>consultada</c> ou
    /// <c>usuario-nao-encontrado</c>.
    /// </summary>
    public void RegistrarConsultaLimites(string resultado) =>
        _consultasLimites.Add(1, new KeyValuePair<string, object?>("resultado", resultado));
}
