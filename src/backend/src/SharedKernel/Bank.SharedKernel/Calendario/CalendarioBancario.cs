using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Bank.SharedKernel.Calendario;

/// <summary>
/// Dias úteis bancários (specs/014-cartao-credito-cobrancas, FR-022a; research R12): segunda a sexta,
/// exceto feriados nacionais fixos e, pela Páscoa, Carnaval (segunda e terça), Sexta-feira Santa e
/// Corpus Christi. Calculado para qualquer ano, sem cadastro.
/// </summary>
public static class CalendarioBancario
{
    private static readonly (int Mes, int Dia)[] FeriadosFixos =
        [(1, 1), (4, 21), (5, 1), (9, 7), (10, 12), (11, 2), (11, 15), (11, 20), (12, 25)];

    /// <summary>Dias a partir da Páscoa: Carnaval (segunda e terça), Sexta-feira Santa e Corpus Christi.</summary>
    private static readonly int[] DeslocamentosDaPascoa = [-48, -47, -2, 60];

    private static readonly ConcurrentDictionary<int, FrozenSet<DateOnly>> PorAno = new();

    public static IReadOnlySet<DateOnly> Feriados(int ano) => PorAno.GetOrAdd(ano, Calcular);

    public static bool EhDiaUtil(DateOnly data) =>
        data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Feriados(data.Year).Contains(data);

    /// <summary>A própria data, se útil; senão, o primeiro dia útil depois dela.</summary>
    public static DateOnly ProximoDiaUtil(DateOnly data)
    {
        while (!EhDiaUtil(data))
        {
            data = data.AddDays(1);
        }

        return data;
    }

    /// <summary>O enésimo dia útil depois da data; a própria data nunca conta.</summary>
    public static DateOnly SomarDiasUteis(DateOnly data, int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);

        for (var contados = 0; contados < quantidade;)
        {
            data = data.AddDays(1);
            if (EhDiaUtil(data))
            {
                contados++;
            }
        }

        return data;
    }

    private static FrozenSet<DateOnly> Calcular(int ano)
    {
        var pascoa = Pascoa(ano);
        return FeriadosFixos.Select(f => new DateOnly(ano, f.Mes, f.Dia))
            .Concat(DeslocamentosDaPascoa.Select(pascoa.AddDays))
            .ToFrozenSet();
    }

    /// <summary>Domingo de Páscoa no calendário gregoriano (Meeus/Jones/Butcher).</summary>
    private static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var (b, c) = Math.DivRem(ano, 100);
        var (d, e) = Math.DivRem(b, 4);
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var (i, k) = Math.DivRem(c, 4);
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var n = h + l - (7 * m) + 114;
        return new DateOnly(ano, n / 31, (n % 31) + 1);
    }
}
