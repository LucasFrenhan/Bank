using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bank.Domain.Cobrancas;

namespace Bank.Infrastructure.Mensageria.Contratos;

/// <summary>
/// Payload do evento <c>cobranca.paga</c> v1 (specs/010-mensageria-rabbitmq, FR-012;
/// specs/014-cartao-credito-cobrancas, contracts/evento-cobranca-paga.md, que acrescentou o
/// <see cref="LojistaNome"/> opcional). Só os 4 últimos dígitos do cartão (constituição, Princípio VI).
/// </summary>
internal sealed partial record CobrancaPagaV1
{
    public const string Tipo = CobrancaPaga.Tipo;
    public const int VersaoAtual = CobrancaPaga.Versao;
    public const string TipoEventoValor = "CobrancaPaga";

    public CobrancaPagaV1(Guid eventoId, Guid cobrancaId, Guid lojistaUsuarioId, Guid pagadorUsuarioId, decimal valor, string ultimosDigitosCartao, DateTimeOffset ocorridoEm, string? lojistaNome = null)
    {
        if (!QuatroDigitos().IsMatch(ultimosDigitosCartao))
        {
            throw new ArgumentException("Informe exatamente os 4 últimos dígitos do cartão.", nameof(ultimosDigitosCartao));
        }

        (EventoId, CobrancaId, LojistaUsuarioId, PagadorUsuarioId, Valor, UltimosDigitosCartao, OcorridoEm, LojistaNome) =
            (eventoId, cobrancaId, lojistaUsuarioId, pagadorUsuarioId, valor, ultimosDigitosCartao, ocorridoEm, lojistaNome);
    }

    public Guid EventoId { get; }

    public string TipoEvento { get; } = TipoEventoValor;

    public int Versao { get; } = VersaoAtual;

    public Guid CobrancaId { get; }

    public Guid LojistaUsuarioId { get; }

    public Guid PagadorUsuarioId { get; }

    public decimal Valor { get; }

    public string UltimosDigitosCartao { get; }

    public DateTimeOffset OcorridoEm { get; }

    /// <summary>Opcional, para o texto da notificação do pagador (spec 014).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LojistaNome { get; }

    public static CobrancaPagaV1 De(CobrancaPaga evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return new(evento.EventoId.Valor, evento.CobrancaId.Valor, evento.LojistaUsuarioId.Valor, evento.PagadorUsuarioId.Valor,
            evento.Valor, evento.UltimosDigitosCartao, evento.OcorridoEm, evento.LojistaNome);
    }

    public string Serializar() => JsonSerializer.Serialize(this, TransferenciaConcluidaV1.Json);

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex QuatroDigitos();
}
