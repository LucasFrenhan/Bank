using System.Text.Json;
using Bank.Domain.Transferencias;

namespace Bank.Infrastructure.Mensageria.Contratos;

/// <summary>
/// Payload do evento <c>transferencia.concluida</c> v1, revisão de 2026-11-21
/// (specs/010-mensageria-rabbitmq/contracts/evento-transferencia-concluida.md). Identificadores
/// como <see cref="Guid"/>, independentes dos tipos do Domain (issue #43). A ordem das
/// propriedades é a do contrato.
/// </summary>
internal sealed record TransferenciaConcluidaV1(
    Guid EventoId,
    string TipoEvento,
    int Versao,
    Guid TransferenciaId,
    Guid PagadorUsuarioId,
    string PagadorNome,
    Guid RecebedorUsuarioId,
    string RecebedorNome,
    decimal Valor,
    string? Descricao,
    DateTimeOffset OcorridoEm)
{
    public const string TipoEventoValor = "TransferenciaConcluida";

    /// <summary>camelCase, UTF-8; campos nulos serializados como <c>null</c>.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TransferenciaConcluidaV1 De(TransferenciaConcluida evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        return new TransferenciaConcluidaV1(
            evento.EventoId.Valor,
            TipoEventoValor,
            TransferenciaConcluida.Versao,
            evento.TransferenciaId.Valor,
            evento.PagadorUsuarioId.Valor,
            evento.PagadorNome,
            evento.RecebedorUsuarioId.Valor,
            evento.RecebedorNome,
            evento.Valor,
            evento.Descricao,
            evento.OcorridoEm);
    }
}
