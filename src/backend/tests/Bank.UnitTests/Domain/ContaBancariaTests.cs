using Bank.Domain.Bancos;

namespace Bank.UnitTests.Domain;

/// <summary>Dados bancários e dígito verificador (specs/012-dados-bancarios-carteira, FR-002, FR-004; research R1).</summary>
public class ContaBancariaTests
{
    [Theory]
    [InlineData("0001", "00000001", "6")]
    [InlineData("0001", "00000002", "4")]
    [InlineData("0001", "00000042", "0")]
    [InlineData("0001", "12345678", "0")]
    [InlineData("0001", "99999999", "6")]
    public void CalcularDigito_VetoresDoContrato(string agencia, string numero, string esperado) =>
        Assert.Equal(esperado, ContaBancaria.CalcularDigito(agencia, numero));

    [Fact]
    public void Nova_FormataComZerosECalculaODigito()
    {
        var conta = ContaBancaria.Nova(Banco.OneBank, "0001", 42, TipoContaBancaria.ContaCorrente);

        Assert.Equal("001", conta.CodigoBanco);
        Assert.Equal("0001", conta.Agencia);
        Assert.Equal("00000042", conta.Numero);
        Assert.Equal("0", conta.Digito);
        Assert.Equal(TipoContaBancaria.ContaCorrente, conta.Tipo);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(100_000_000L)]
    public void Nova_NumeroForaDaFaixa_Recusa(long numero) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ContaBancaria.Nova(Banco.OneBank, "0001", numero, TipoContaBancaria.ContaCorrente));

    [Theory]
    [InlineData("001")]
    [InlineData("00001")]
    [InlineData("00A1")]
    [InlineData("")]
    public void Nova_AgenciaInvalida_Recusa(string agencia) =>
        Assert.Throws<ArgumentException>(() => ContaBancaria.Nova(Banco.OneBank, agencia, 1, TipoContaBancaria.ContaCorrente));

    [Fact]
    public void Nova_TipoInexistente_Recusa() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ContaBancaria.Nova(Banco.OneBank, "0001", 1, (TipoContaBancaria)99));

    [Fact]
    public void CalcularDigito_DetectaQualquerErroEmUmDigito()
    {
        const string original = "000112345678";
        var digito = ContaBancaria.CalcularDigito(original[..4], original[4..]);

        for (var posicao = 0; posicao < original.Length; posicao++)
        {
            for (var valor = '0'; valor <= '9'; valor++)
            {
                if (valor == original[posicao])
                {
                    continue;
                }

                var alterado = original[..posicao] + valor + original[(posicao + 1)..];
                Assert.NotEqual(digito, ContaBancaria.CalcularDigito(alterado[..4], alterado[4..]));
            }
        }
    }

    [Fact]
    public void Reconstituir_MantemOsValores()
    {
        var conta = ContaBancaria.Reconstituir("001", "0001", "00000002", "4", TipoContaBancaria.ContaPoupanca);

        Assert.Equal(ContaBancaria.Nova(Banco.OneBank, "0001", 2, TipoContaBancaria.ContaPoupanca), conta);
    }
}
