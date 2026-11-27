using Bank.Domain.Sessoes;

namespace Bank.UnitTests.Domain;

/// <summary>Hash da credencial (specs/016-sessao-refresh-token, FR-004).</summary>
public sealed class HashCredencialTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void De_TamanhoDiferenteDe32_Lanca(int tamanho)
    {
        Assert.Throws<ArgumentException>(() => HashCredencial.De(new byte[tamanho]));
    }

    [Fact]
    public void Igualdade_PorBytes()
    {
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var outro = (byte[])bytes.Clone();
        outro[31] = 99;

        Assert.Equal(HashCredencial.De(bytes), HashCredencial.De(bytes));
        Assert.True(HashCredencial.De(bytes) == HashCredencial.De((byte[])bytes.Clone()));
        Assert.NotEqual(HashCredencial.De(bytes), HashCredencial.De(outro));
        Assert.Equal(HashCredencial.De(bytes).GetHashCode(), HashCredencial.De(bytes).GetHashCode());
    }

    [Fact]
    public void Bytes_DevolveCopia_EToStringNaoExpoe()
    {
        var bytes = Enumerable.Repeat((byte)0xAB, 32).ToArray();
        var hash = HashCredencial.De(bytes);

        hash.Bytes[0] = 0;

        Assert.Equal(0xAB, hash.Bytes[0]);
        Assert.DoesNotContain("AB", hash.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("171", hash.ToString(), StringComparison.Ordinal);
    }
}
