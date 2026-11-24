using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Seguranca;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Derivação do número e do CVV (specs/014-cartao-credito-cobrancas, FR-004, FR-008; research R1, R3).</summary>
public class DadosCartaoHmacTests
{
    private static readonly ValidadeCartao Validade = ValidadeCartao.Reconstituir(2031, 11);
    private static readonly CartaoId Id = CartaoId.De(Guid.Parse("019a8f77-5555-7890-abcd-ef1234567890"));

    private readonly DadosCartaoHmac _dados = Criar("chave-de-teste-com-mais-de-32-bytes-0123456789");

    [Fact]
    public void Derivar_EhDeterministico()
    {
        var a = _dados.Derivar(Id, Bandeira.Visa, 0, Validade);
        var b = _dados.Derivar(Id, Bandeira.Visa, 0, Validade);

        Assert.Equal(a.Numero.Valor, b.Numero.Valor);
        Assert.Equal(a.Cvv.Valor, b.Cvv.Valor);
        Assert.Equal(a.ImpressaoDigital, b.ImpressaoDigital);
    }

    [Theory]
    [InlineData("001", "400000")]
    [InlineData("002", "555555")]
    public void Derivar_PrefixoDaBandeiraELuhn(string codigo, string prefixo)
    {
        var dados = _dados.Derivar(Id, Bandeira.DeCodigo(codigo)!, 0, Validade);

        Assert.StartsWith(prefixo, dados.Numero.Valor, StringComparison.Ordinal);
        Assert.True(NumeroCartao.Criar(dados.Numero.Valor).Sucesso);
        Assert.Equal(codigo, Bandeira.DoNumero(dados.Numero)!.Codigo);
        Assert.Matches("^[0-9]{3}$", dados.Cvv.Valor);
    }

    [Fact]
    public void Derivar_VariacaoCartaoOuChaveDiferentes_MudamONumero()
    {
        var original = _dados.Derivar(Id, Bandeira.Visa, 0, Validade).Numero.Valor;

        Assert.NotEqual(original, _dados.Derivar(Id, Bandeira.Visa, 1, Validade).Numero.Valor);
        Assert.NotEqual(original, _dados.Derivar(CartaoId.Novo(DateTimeOffset.UtcNow), Bandeira.Visa, 0, Validade).Numero.Valor);
        Assert.NotEqual(original, Criar("outra-chave-de-teste-com-mais-de-32-bytes-01234").Derivar(Id, Bandeira.Visa, 0, Validade).Numero.Valor);
    }

    [Fact]
    public void Derivar_CvvSempreComTresDigitos_InclusiveComZerosAEsquerda()
    {
        var cvvs = Enumerable.Range(0, 300).Select(_ => _dados.Derivar(CartaoId.Novo(DateTimeOffset.UtcNow), Bandeira.Visa, 0, Validade).Cvv.Valor).ToList();

        Assert.All(cvvs, c => Assert.Matches("^[0-9]{3}$", c));
        Assert.Contains(cvvs, c => c[0] == '0');
    }

    [Fact]
    public void ImpressaoDigital_NaoContemONumero_ETextoDoRegistroEhMascarado()
    {
        var dados = _dados.Derivar(Id, Bandeira.Visa, 0, Validade);

        Assert.DoesNotContain(dados.Numero.Valor, dados.ImpressaoDigital, StringComparison.Ordinal);
        Assert.Equal(44, dados.ImpressaoDigital.Length);
        Assert.DoesNotContain(dados.Numero.Valor, dados.ToString(), StringComparison.Ordinal);
        Assert.Contains("Cvv = ***", dados.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Recuperar_DevolveOsMesmosDados_EConferem()
    {
        var (cartao, dados) = Emitir();

        Assert.Equal(dados.Numero.Valor, _dados.Recuperar(cartao).Numero.Valor);
        Assert.True(_dados.Conferem(cartao, dados.Numero, Validade, dados.Cvv));
    }

    [Fact]
    public void Conferem_QualquerDadoDiferente_Falso()
    {
        var (cartao, dados) = Emitir();
        var outroCvv = Cvv.Criar(dados.Cvv.Valor == "000" ? "001" : "000").Valor;

        Assert.False(_dados.Conferem(cartao, CartoesDeTeste.Numero, Validade, dados.Cvv));
        Assert.False(_dados.Conferem(cartao, dados.Numero, ValidadeCartao.Reconstituir(2031, 12), dados.Cvv));
        Assert.False(_dados.Conferem(cartao, dados.Numero, Validade, outroCvv));
    }

    [Fact]
    public void ChaveTrocada_FalhaSemRevelarDados()
    {
        var (cartao, dados) = Emitir();
        var outra = Criar("outra-chave-de-teste-com-mais-de-32-bytes-01234");

        var erro = Assert.Throws<InvalidOperationException>(() => outra.Recuperar(cartao));

        Assert.DoesNotContain(dados.Numero.Valor, erro.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("curta-demais", false)]
    [InlineData("0123456789012345678901234567890", false)]
    [InlineData("01234567890123456789012345678901", true)]
    public void Chave_ExigeAoMenos32Bytes(string chave, bool valida) =>
        Assert.Equal(valida, new ValidadorChaveCartoesOptions().Validate(null, new ChaveCartoesOptions { ChaveDerivacao = chave }).Succeeded);

    private (Cartao Cartao, Bank.Application.Abstracoes.DadosDerivados Dados) Emitir()
    {
        var dados = _dados.Derivar(Id, Bandeira.Visa, 0, Validade);
        var cartao = Cartao.Emitir(Id, UsuarioId.Novo(DateTimeOffset.UtcNow), Bandeira.Visa, "Lucas Silva", Validade, 0,
            dados.Numero.UltimosDigitos, dados.ImpressaoDigital, 5000m, DateTimeOffset.UtcNow);
        return (cartao, dados);
    }

    private static DadosCartaoHmac Criar(string chave) => new(Options.Create(new ChaveCartoesOptions { ChaveDerivacao = chave }));
}
