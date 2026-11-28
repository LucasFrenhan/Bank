using System.Globalization;
using Bank.SharedKernel;

namespace Bank.Domain.Cartoes;

/// <summary>Mês e ano de validade; o cartão vale até o fim do mês (specs/014-cartao-credito-cobrancas, FR-004, FR-015).</summary>
public sealed record ValidadeCartao
{
    public const string Campo = "validade";
    public const int MesesDeValidade = 60;

    private ValidadeCartao(int ano, int mes)
    {
        Ano = ano;
        Mes = mes;
    }

    public int Ano { get; }

    public int Mes { get; }

    /// <summary>Validade de um cartão emitido no dia: mês da emissão + 60 meses.</summary>
    public static ValidadeCartao DaEmissao(DateOnly hoje)
    {
        var fim = hoje.AddMonths(MesesDeValidade);
        return new ValidadeCartao(fim.Year, fim.Month);
    }

    public static ValidadeCartao Reconstituir(int ano, int mes) => new(ano, mes);

    /// <summary>Lê <c>MM/AA</c> (século 2000).</summary>
    public static Resultado<ValidadeCartao> Criar(string? texto) =>
        texto is { Length: 5 } && texto[2] == '/'
            && int.TryParse(texto.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var mes)
            && int.TryParse(texto.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var ano)
            && mes is >= 1 and <= 12
            ? Resultado.Ok(new ValidadeCartao(2000 + ano, mes))
            : Resultado.Falha<ValidadeCartao>(Erro.Validacao(Campo, "Informe a validade no formato MM/AA."));

    public bool EstaVencida(DateOnly hoje) => (hoje.Year, hoje.Month).CompareTo((Ano, Mes)) > 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Mes:D2}/{Ano % 100:D2}");
}
