using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bank.Infrastructure.Mensageria.Contratos;

/// <summary>
/// Payload do evento <c>cobranca.paga</c> v1 — preparação para a Fase 4
/// (specs/010-mensageria-rabbitmq, FR-012; contracts/evento-cobranca-paga.md). Ainda sem produtor
/// nem consumidor: a spec de cobranças o usará na primeira publicação. Só os 4 últimos dígitos do
/// cartão (constituição, Princípio VI).
/// </summary>
internal sealed partial record CobrancaPagaV1
{
    public const string Tipo = "cobranca.paga";
    public const int VersaoAtual = 1;
    public const string TipoEventoValor = "CobrancaPaga";

    public CobrancaPagaV1(Guid eventoId, Guid cobrancaId, Guid lojistaUsuarioId, Guid pagadorUsuarioId, decimal valor, string ultimosDigitosCartao, DateTimeOffset ocorridoEm)
    {
        if (!QuatroDigitos().IsMatch(ultimosDigitosCartao))
        {
            throw new ArgumentException("Informe exatamente os 4 últimos dígitos do cartão.", nameof(ultimosDigitosCartao));
        }

        (EventoId, CobrancaId, LojistaUsuarioId, PagadorUsuarioId, Valor, UltimosDigitosCartao, OcorridoEm) =
            (eventoId, cobrancaId, lojistaUsuarioId, pagadorUsuarioId, valor, ultimosDigitosCartao, ocorridoEm);
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

    public string Serializar() => JsonSerializer.Serialize(this, TransferenciaConcluidaV1.Json);

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex QuatroDigitos();
}
