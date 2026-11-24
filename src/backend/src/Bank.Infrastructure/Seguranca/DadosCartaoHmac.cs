using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Seguranca;

/// <summary>
/// Deriva número, CVV e impressão digital por HMAC-SHA256, com uma finalidade em cada entrada
/// (specs/014-cartao-credito-cobrancas, research R1). Os números são fictícios.
/// </summary>
internal sealed class DadosCartaoHmac(IOptions<ChaveCartoesOptions> opcoes) : IDadosCartao
{
    private const string PrefixoVisa = "400000";
    private const string PrefixoMastercard = "555555";

    private readonly byte[] _chave = Encoding.UTF8.GetBytes(opcoes.Value.ChaveDerivacao);

    public DadosDerivados Derivar(CartaoId cartaoId, Bandeira bandeira, int variacao, ValidadeCartao validade)
    {
        ArgumentNullException.ThrowIfNull(bandeira);
        ArgumentNullException.ThrowIfNull(validade);

        var prefixo = bandeira.Codigo switch
        {
            Bandeira.CodigoVisa => PrefixoVisa,
            Bandeira.CodigoMastercard => PrefixoMastercard,
            _ => throw new ArgumentOutOfRangeException(nameof(bandeira), bandeira.Codigo, "Bandeira sem prefixo definido."),
        };
        var conta = Digitos(Texto($"numero|{cartaoId}|{variacao}"), 9);
        var numero = NumeroCartao.DePrefixo(prefixo + conta);
        var cvv = Cvv.Criar(Digitos(Texto($"cvv|{cartaoId}|{numero.Valor}|{validade}"), Cvv.Tamanho)).Valor;

        return new DadosDerivados(numero, cvv, Convert.ToBase64String(Hmac(Texto($"impressao|{numero.Valor}"))));
    }

    public DadosDerivados Recuperar(Cartao cartao)
    {
        ArgumentNullException.ThrowIfNull(cartao);

        var bandeira = Bandeira.DeCodigo(cartao.CodigoBandeira)
            ?? throw new InvalidOperationException("O cartão tem uma bandeira desconhecida.");
        var dados = Derivar(cartao.Id, bandeira, cartao.Variacao, cartao.Validade);

        return string.Equals(dados.ImpressaoDigital, cartao.ImpressaoDigital, StringComparison.Ordinal)
            ? dados
            : throw new InvalidOperationException("Os dados derivados não correspondem ao cartão gravado: a chave de derivação mudou.");
    }

    public bool Conferem(Cartao cartao, NumeroCartao numero, ValidadeCartao validade, Cvv cvv)
    {
        ArgumentNullException.ThrowIfNull(cartao);
        ArgumentNullException.ThrowIfNull(numero);
        ArgumentNullException.ThrowIfNull(validade);
        ArgumentNullException.ThrowIfNull(cvv);

        var dados = Recuperar(cartao);

        // Sem curto-circuito: o tempo não revela qual dado diverge (R3).
        return Iguais(dados.Numero.Valor, numero.Valor)
            & Iguais(cartao.Validade.ToString(), validade.ToString())
            & Iguais(dados.Cvv.Valor, cvv.Valor);
    }

    private static bool Iguais(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static string Texto(FormattableString texto) => FormattableString.Invariant(texto);

    private string Digitos(string entrada, int quantidade)
    {
        var numero = new BigInteger(Hmac(entrada), isUnsigned: true, isBigEndian: true) % BigInteger.Pow(10, quantidade);
        return numero.ToString("D" + quantidade.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private byte[] Hmac(string entrada) => HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(entrada));
}
