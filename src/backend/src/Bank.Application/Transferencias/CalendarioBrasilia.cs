namespace Bank.Application.Transferencias;

/// <summary>Dia civil de Brasília em UTC: <c>[InicioUtc, FimUtc)</c>.</summary>
public sealed record DiaBrasilia(DateOnly Data, DateTimeOffset InicioUtc, DateTimeOffset FimUtc)
{
    public bool Contem(DateTimeOffset instante) => instante >= InicioUtc && instante < FimUtc;
}

/// <summary>
/// Dia civil de Brasília para o limite diário (specs/009-limite-transferencia, FR-002; research
/// R4). Usa o fuso real (não um deslocamento fixo) para continuar certo se o horário de verão
/// voltar. O ID IANA existe no Linux com <c>tzdata</c>; no Windows com
/// <c>InvariantGlobalization</c>, só o ID do Windows. Sem nenhum dos dois, falha ao construir — o
/// <c>Program.cs</c> pede o singleton na inicialização para a API não subir sem fuso.
/// </summary>
public sealed class CalendarioBrasilia
{
    private static readonly string[] Identificadores = ["America/Sao_Paulo", "E. South America Standard Time"];

    private readonly TimeZoneInfo _fuso = ResolverFuso();

    public DiaBrasilia Hoje(DateTimeOffset agoraUtc)
    {
        var data = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agoraUtc, _fuso).DateTime);
        return new DiaBrasilia(data, InicioEmUtc(data), InicioEmUtc(data.AddDays(1)));
    }

    private DateTimeOffset InicioEmUtc(DateOnly data)
    {
        var meiaNoite = data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Com horário de verão começando à meia-noite (como no Brasil até 2019), 00h00 não existe:
        // o dia começa no primeiro horário válido.
        while (_fuso.IsInvalidTime(meiaNoite))
        {
            meiaNoite = meiaNoite.AddMinutes(30);
        }

        return new DateTimeOffset(meiaNoite, _fuso.GetUtcOffset(meiaNoite)).ToUniversalTime();
    }

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in Identificadores)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var fuso))
            {
                return fuso;
            }
        }

        throw new InvalidOperationException(
            $"Fuso de Brasília não encontrado ({string.Join(", ", Identificadores)}). No contêiner, instale o pacote tzdata.");
    }
}
