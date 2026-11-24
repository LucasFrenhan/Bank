using Bank.Domain.Cartoes;

namespace Bank.UnitTests.Domain;

/// <summary>Value objects do cartão (specs/014-cartao-credito-cobrancas, FR-004, FR-008, FR-015).</summary>
public class DadosDeCartaoTests
{
    [Theory]
    [InlineData("4000001234567899")]
    [InlineData("4000 0012 3456 7899")]
    public void Numero_Valido(string texto)
    {
        var numero = NumeroCartao.Criar(texto);

        Assert.True(numero.Sucesso);
        Assert.Equal("4000001234567899", numero.Valor.Valor);
        Assert.Equal("7899", numero.Valor.UltimosDigitos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4000001234567890")] // Luhn errado
    [InlineData("400000123456789")]
    [InlineData("40000012345678990")]
    [InlineData("400000123456789A")]
    public void Numero_Invalido_ErroNoCampoSemRepetirOValor(string? texto)
    {
        var erro = Assert.Single(NumeroCartao.Criar(texto).Erros);

        Assert.Equal(NumeroCartao.Campo, erro.Campo);
        if (!string.IsNullOrEmpty(texto))
        {
            Assert.DoesNotContain(texto, erro.Mensagem, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Numero_TodoErroDeUmDigitoEDetectado()
    {
        const string Certo = "4000001234567899";
        for (var i = 0; i < Certo.Length; i++)
        {
            for (var d = '0'; d <= '9'; d++)
            {
                if (d != Certo[i])
                {
                    Assert.False(NumeroCartao.Criar(Certo[..i] + d + Certo[(i + 1)..]).Sucesso);
                }
            }
        }
    }

    [Fact]
    public void DePrefixo_CompletaComLuhn() =>
        Assert.Equal("4000001234567899", NumeroCartao.DePrefixo("400000123456789").Valor);

    [Fact]
    public void ToString_NuncaMostraNumeroNemCvv()
    {
        Assert.Equal("**** **** **** 7899", NumeroCartao.DePrefixo("400000123456789").ToString());
        Assert.Equal("***", Cvv.Criar("123").Valor.ToString());
    }

    [Theory]
    [InlineData("123", true)]
    [InlineData("007", true)]
    [InlineData("12", false)]
    [InlineData("1234", false)]
    [InlineData("12a", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Cvv_TemExatamenteTresDigitos(string? texto, bool valido)
    {
        var cvv = Cvv.Criar(texto);

        Assert.Equal(valido, cvv.Sucesso);
        if (!valido)
        {
            Assert.Equal(Cvv.Campo, Assert.Single(cvv.Erros).Campo);
        }
    }

    [Theory]
    [InlineData("11/31", 2031, 11)]
    [InlineData("01/00", 2000, 1)]
    public void Validade_Valida(string texto, int ano, int mes)
    {
        var validade = ValidadeCartao.Criar(texto).Valor;

        Assert.Equal((ano, mes), (validade.Ano, validade.Mes));
        Assert.Equal(texto, validade.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("13/31")]
    [InlineData("00/31")]
    [InlineData("1/31")]
    [InlineData("11/2031")]
    [InlineData("11-31")]
    [InlineData("ab/cd")]
    public void Validade_Invalida(string? texto) =>
        Assert.Equal(ValidadeCartao.Campo, Assert.Single(ValidadeCartao.Criar(texto).Erros).Campo);

    [Fact]
    public void Validade_ValeAteOFimDoMes()
    {
        var validade = ValidadeCartao.Reconstituir(2031, 11);

        Assert.False(validade.EstaVencida(new DateOnly(2031, 11, 30)));
        Assert.True(validade.EstaVencida(new DateOnly(2031, 12, 1)));
        Assert.False(validade.EstaVencida(new DateOnly(2030, 12, 31)));
    }

    [Fact]
    public void Validade_DaEmissao_CincoAnos()
    {
        Assert.Equal("11/31", ValidadeCartao.DaEmissao(new DateOnly(2026, 11, 24)).ToString());
        Assert.Equal("01/32", ValidadeCartao.DaEmissao(new DateOnly(2027, 1, 31)).ToString());
    }

    [Fact]
    public void CodigoAutorizacao_SeisCaracteresAlfanumericos()
    {
        var codigos = Enumerable.Range(0, 100).Select(_ => CodigoAutorizacao.Novo()).ToList();

        Assert.All(codigos, c => Assert.Matches("^[A-Z0-9]{6}$", c));
        Assert.True(codigos.Distinct().Count() > 95);
    }

    [Theory]
    [InlineData("001", "Visa")]
    [InlineData("002", "Mastercard")]
    public void Bandeira_PeloCodigo(string codigo, string nome) =>
        Assert.Equal(nome, Bandeira.DeCodigo(codigo)!.Nome);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("003")]
    public void Bandeira_Desconhecida_Nula(string? codigo) => Assert.Null(Bandeira.DeCodigo(codigo));

    [Theory]
    [InlineData("400000123456789", "001")]
    [InlineData("555555123456789", "002")]
    [InlineData("300000123456789", null)]
    public void Bandeira_PeloNumero(string prefixo, string? codigo) =>
        Assert.Equal(codigo, Bandeira.DoNumero(NumeroCartao.DePrefixo(prefixo))?.Codigo);
}
