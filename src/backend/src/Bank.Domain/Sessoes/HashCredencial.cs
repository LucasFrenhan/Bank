namespace Bank.Domain.Sessoes;

/// <summary>
/// SHA-256 (32 bytes) de uma credencial de renovação (specs/016-sessao-refresh-token, FR-004; research R1).
/// Nunca guarda o valor original; a igualdade é por bytes e <see cref="ToString"/> não expõe o conteúdo.
/// </summary>
public sealed class HashCredencial : IEquatable<HashCredencial>
{
    public const int Tamanho = 32;

    private readonly byte[] _bytes;

    private HashCredencial(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>Cópia dos bytes do hash (para persistência).</summary>
    public byte[] Bytes => (byte[])_bytes.Clone();

    public static HashCredencial De(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Tamanho)
        {
            throw new ArgumentException($"O hash da credencial deve ter {Tamanho} bytes.", nameof(bytes));
        }

        return new HashCredencial(bytes.ToArray());
    }

    public bool Equals(HashCredencial? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => Equals(obj as HashCredencial);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    public override string ToString() => "HashCredencial(***)";

    public static bool operator ==(HashCredencial? a, HashCredencial? b) => a is null ? b is null : a.Equals(b);

    public static bool operator !=(HashCredencial? a, HashCredencial? b) => !(a == b);
}
