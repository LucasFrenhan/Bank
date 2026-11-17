using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Bank.UnitTests.Application;

/// <summary>Cria uma classe de métricas com um <see cref="IMeterFactory"/> próprio e captura as medições.</summary>
internal sealed class MetricasCapturadasBase : IDisposable
{
    private readonly Fabrica _fabrica = new();
    private readonly MeterListener _ouvinte = new();
    private readonly ConcurrentQueue<(string Nome, double Valor, Dictionary<string, string?> Tags)> _medicoes = new();

    public MetricasCapturadasBase(Func<IMeterFactory, object> criar)
    {
        ArgumentNullException.ThrowIfNull(criar);
        Instancia = criar(_fabrica);

        _ouvinte.InstrumentPublished = (instrumento, ouvinte) =>
        {
            if (_fabrica.Meters.Contains(instrumento.Meter))
            {
                ouvinte.EnableMeasurementEvents(instrumento);
            }
        };
        _ouvinte.SetMeasurementEventCallback<long>((i, v, tags, _) => Registrar(i.Name, v, tags));
        _ouvinte.SetMeasurementEventCallback<double>((i, v, tags, _) => Registrar(i.Name, v, tags));
        _ouvinte.Start();
    }

    public object Instancia { get; }

    public IReadOnlyCollection<(string Nome, double Valor, Dictionary<string, string?> Tags)> Medicoes => [.. _medicoes];

    public void Dispose()
    {
        _ouvinte.Dispose();
        _fabrica.Dispose();
    }

    private void Registrar(string nome, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        _medicoes.Enqueue((nome, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));

    private sealed class Fabrica : IMeterFactory
    {
        public List<Meter> Meters { get; } = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            Meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in Meters)
            {
                meter.Dispose();
            }
        }
    }
}
