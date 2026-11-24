using System.Diagnostics.Metrics;

namespace Bank.Application.Observabilidade;

/// <summary>Métricas do processamento diário (specs/014-cartao-credito-cobrancas, FR-037; plan, Observabilidade).</summary>
public sealed class MetricasProcessamento
{
    public const string NomeMeter = "Bank.Processamento";
    public const string EtapaLancamento = "lancamento";
    public const string EtapaLiquidacao = "liquidacao";

    private readonly Counter<long> _execucoes;
    private readonly Counter<long> _comprasLancadas;
    private readonly Counter<long> _faturasLiberadas;
    private readonly Counter<long> _recebiveisLiquidados;
    private readonly Counter<long> _falhas;
    private readonly Histogram<double> _duracao;

    public MetricasProcessamento(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _execucoes = meter.CreateCounter<long>("bank.processamento.execucoes", "{execucao}", "Rodadas com trabalho, por resultado.");
        _comprasLancadas = meter.CreateCounter<long>("bank.processamento.compras_lancadas", "{compra}", "Compras lançadas em fatura.");
        _faturasLiberadas = meter.CreateCounter<long>("bank.processamento.faturas_liberadas", "{fatura}", "Faturas liberadas para pagamento.");
        _recebiveisLiquidados = meter.CreateCounter<long>("bank.processamento.recebiveis_liquidados", "{recebivel}", "Recebíveis creditados ao lojista.");
        _falhas = meter.CreateCounter<long>("bank.processamento.falhas", "{item}", "Itens que falharam, por etapa.");
        _duracao = meter.CreateHistogram<double>("bank.processamento.duracao", "s", "Duração das rodadas com trabalho.");
    }

    public void RegistrarLancamentos(int compras, int faturasLiberadas)
    {
        _comprasLancadas.Add(compras);
        _faturasLiberadas.Add(faturasLiberadas);
    }

    public void RegistrarLiquidado() => _recebiveisLiquidados.Add(1);

    public void RegistrarFalha(string etapa) => _falhas.Add(1, new KeyValuePair<string, object?>("etapa", etapa));

    public void RegistrarExecucao(bool comFalhas, TimeSpan duracao)
    {
        _execucoes.Add(1, new KeyValuePair<string, object?>("resultado", comFalhas ? "com-falhas" : "concluida"));
        _duracao.Record(duracao.TotalSeconds);
    }
}
