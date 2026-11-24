using System.Diagnostics.Metrics;
using Bank.Domain.Cartoes;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas de cartões, cobranças, faturas e recebíveis (specs/014-cartao-credito-cobrancas, FR-037). As
/// tags nunca carregam dados pessoais nem de cartão.
/// </summary>
public sealed class MetricasCartoes
{
    public const string NomeMeter = "Bank.Cartoes";

    private readonly Counter<long> _emitidos;
    private readonly Counter<long> _cobrancasEmitidas;
    private readonly Counter<long> _cobrancasCanceladas;
    private readonly Counter<long> _autorizacoes;
    private readonly Counter<long> _faturasPagas;
    private readonly Counter<long> _recebiveisAntecipados;

    public MetricasCartoes(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _emitidos = meter.CreateCounter<long>("bank.cartoes.emitidos", "{cartao}", "Cartões emitidos, por bandeira.");
        _cobrancasEmitidas = meter.CreateCounter<long>("bank.cobrancas.emitidas", "{cobranca}", "Cobranças emitidas.");
        _cobrancasCanceladas = meter.CreateCounter<long>("bank.cobrancas.canceladas", "{cobranca}", "Cobranças canceladas.");
        _autorizacoes = meter.CreateCounter<long>("bank.cobrancas.autorizacoes", "{autorizacao}", "Decisões da bandeira, por resultado e bandeira.");
        _faturasPagas = meter.CreateCounter<long>("bank.faturas.pagamentos", "{fatura}", "Faturas pagas, em dia ou vencidas.");
        _recebiveisAntecipados = meter.CreateCounter<long>("bank.recebiveis.antecipados", "{recebivel}", "Recebíveis antecipados.");
    }

    public void RegistrarEmitido(string codigoBandeira) => _emitidos.Add(1, Tag("bandeira", codigoBandeira));

    public void RegistrarCobrancaEmitida() => _cobrancasEmitidas.Add(1);

    public void RegistrarCobrancaCancelada() => _cobrancasCanceladas.Add(1);

    public void RegistrarAutorizacao(AutorizacaoCartao autorizacao)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);

        var resultado = autorizacao.Motivo is { } motivo ? AutorizadorBandeira.ErroDe(motivo).Codigo : "aprovada";
        _autorizacoes.Add(1, Tag("resultado", resultado), Tag("bandeira", autorizacao.CodigoBandeira ?? "nenhuma"));
    }

    public void RegistrarFaturaPaga(bool vencida) => _faturasPagas.Add(1, Tag("vencida", vencida));

    public void RegistrarRecebivelAntecipado() => _recebiveisAntecipados.Add(1);

    private static KeyValuePair<string, object?> Tag(string nome, object valor) => new(nome, valor);
}
