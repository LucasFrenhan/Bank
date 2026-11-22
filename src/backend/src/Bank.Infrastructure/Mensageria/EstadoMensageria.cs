using System.Collections.Concurrent;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Estado dos consumidores para a prontidão do Worker (<c>/health/pronto</c>;
/// specs/010-mensageria-rabbitmq, FR-024): pronto quando todos os consumidores registrados estão
/// inscritos na fila (o que implica conexão aberta).
/// </summary>
public sealed class EstadoMensageria
{
    private readonly ConcurrentDictionary<string, bool> _inscritos = new(StringComparer.Ordinal);

    public bool Pronto => !_inscritos.IsEmpty && _inscritos.Values.All(inscrito => inscrito);

    public IReadOnlyDictionary<string, bool> Consumidores => _inscritos;

    internal void Registrar(string consumidor) => _inscritos.TryAdd(consumidor, false);

    internal void MarcarInscrito(string consumidor, bool inscrito) => _inscritos[consumidor] = inscrito;
}
