using System.Security.Cryptography;
using System.Text;
using Bank.Domain.Sessoes;
using Bank.Infrastructure.Seguranca;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Credenciais de renovação (specs/016-sessao-refresh-token, FR-003, FR-004; research R1).</summary>
public sealed class GeradorCredencialAleatoriaTests
{
    private readonly GeradorCredencialAleatoria _gerador = new();

    [Fact]
    public void Gerar_Base64UrlDe43Caracteres()
    {
        var credencial = _gerador.Gerar();

        Assert.Equal(43, credencial.Valor.Length);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", credencial.Valor);
    }

    [Fact]
    public void Gerar_MilValoresDistintos()
    {
        var valores = Enumerable.Range(0, 1000).Select(_ => _gerador.Gerar().Valor).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1000, valores.Count);
    }

    [Fact]
    public void CalcularHash_Sha256DoValor_IgualAoGeradoJunto()
    {
        var credencial = _gerador.Gerar();

        Assert.Equal(credencial.Hash, _gerador.CalcularHash(credencial.Valor));
        Assert.Equal(HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(credencial.Valor))), credencial.Hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é base64 !!!")]
    [InlineData("x")]
    public void CalcularHash_ValorMalformado_NaoLanca(string valor)
    {
        Assert.Equal(HashCredencial.Tamanho, _gerador.CalcularHash(valor).Bytes.Length);
    }
}
