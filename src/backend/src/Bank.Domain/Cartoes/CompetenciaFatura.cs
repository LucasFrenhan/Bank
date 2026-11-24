using System.Globalization;
using Bank.Domain.Calendario;
using Bank.Domain.Comum;

namespace Bank.Domain.Cartoes;

/// <summary>
/// Mês de fechamento e vencimento de uma fatura (specs/014-cartao-credito-cobrancas, FR-024;
/// research R5): reúne as compras de 08 do mês anterior a 07 do mês e vence no dia 10 ou no próximo
/// dia útil.
/// </summary>
public readonly record struct CompetenciaFatura
{
    public const string Campo = "competencia";
    public const int DiaFechamento = 7;
    public const int DiaVencimento = 10;

    private CompetenciaFatura(int ano, int mes)
    {
        Ano = ano;
        Mes = mes;
    }

    public int Ano { get; }

    public int Mes { get; }

    public DateOnly PrimeiroDiaDoMes => new(Ano, Mes, 1);

    public DateOnly Inicio => PrimeiroDiaDoMes.AddMonths(-1).AddDays(DiaFechamento);

    public DateOnly Fechamento => new(Ano, Mes, DiaFechamento);

    public DateOnly Vencimento => CalendarioBancario.ProximoDiaUtil(new DateOnly(Ano, Mes, DiaVencimento));

    /// <summary>Competência da compra feita no dia.</summary>
    public static CompetenciaFatura De(DateOnly dataCompra)
    {
        var mes = dataCompra.Day <= DiaFechamento ? dataCompra : dataCompra.AddMonths(1);
        return new CompetenciaFatura(mes.Year, mes.Month);
    }

    public static CompetenciaFatura DoMes(DateOnly mes) => new(mes.Year, mes.Month);

    /// <summary>Lê <c>AAAA-MM</c>.</summary>
    public static Resultado<CompetenciaFatura> Criar(string? texto) =>
        DateOnly.TryParseExact(texto + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mes)
            && mes.Year >= 2000
            ? Resultado.Ok(DoMes(mes))
            : Resultado.Falha<CompetenciaFatura>(Erro.Validacao(Campo, "Informe a competência no formato AAAA-MM."));

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Ano:D4}-{Mes:D2}");
}
