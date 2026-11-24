namespace Bank.Domain.Cartoes;

/// <summary>
/// Juros simples por dia de atraso (specs/014-cartao-credito-cobrancas, FR-028a; research R13):
/// total × (taxa mensal ÷ 30) × dias, com 2 casas e meio centavo para cima.
/// </summary>
public static class JurosAtraso
{
    private const int DiasDoMes = 30;

    public static decimal Calcular(decimal total, decimal taxaMensalPercentual, int diasAtraso)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        ArgumentOutOfRangeException.ThrowIfNegative(taxaMensalPercentual);
        ArgumentOutOfRangeException.ThrowIfNegative(diasAtraso);

        // Multiplica antes de dividir, para não perder precisão.
        return decimal.Round(total * taxaMensalPercentual * diasAtraso / (100m * DiasDoMes), 2, MidpointRounding.AwayFromZero) + 0.00m;
    }
}
