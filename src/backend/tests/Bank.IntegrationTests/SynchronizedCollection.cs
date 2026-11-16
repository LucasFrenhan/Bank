using System.Collections;

namespace Bank.IntegrationTests;

/// <summary>
/// Coleção segura para escrita pelo exportador de telemetria (outra thread) e leitura
/// pelos testes. Leituras devem usar <see cref="Copiar"/>.
/// </summary>
public sealed class SynchronizedCollection<T> : ICollection<T>
{
    private readonly List<T> _itens = [];
    private readonly Lock _trava = new();

    public int Count { get { lock (_trava) { return _itens.Count; } } }

    public bool IsReadOnly => false;

    public void Add(T item) { lock (_trava) { _itens.Add(item); } }

    public void Clear() { lock (_trava) { _itens.Clear(); } }

    public bool Contains(T item) { lock (_trava) { return _itens.Contains(item); } }

    public void CopyTo(T[] array, int arrayIndex) { lock (_trava) { _itens.CopyTo(array, arrayIndex); } }

    public bool Remove(T item) { lock (_trava) { return _itens.Remove(item); } }

    public IReadOnlyList<T> Copiar() { lock (_trava) { return [.. _itens]; } }

    public IEnumerator<T> GetEnumerator() => Copiar().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
