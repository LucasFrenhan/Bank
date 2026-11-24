using System.Globalization;
using System.Text.Json;
using Bank.Application.Abstracoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria.Contratos;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Lê o contrato <c>cobranca.paga</c> v1 (specs/014-cartao-credito-cobrancas, contracts/evento-cobranca-paga.md)
/// com as mesmas regras de <see cref="LeitorTransferenciaConcluida"/>: campo obrigatório ausente, tipo ou versão
/// diferente tornam a mensagem inválida. Os dígitos do cartão são ignorados.
/// </summary>
internal static class LeitorCobrancaPaga
{
    public static LeituraMensagem<NotificacaoCobranca> Ler(ReadOnlyMemory<byte> corpo, IReadOnlyBasicProperties propriedades)
    {
        ArgumentNullException.ThrowIfNull(propriedades);

        Guid? idDaMensagem = Guid.TryParse(propriedades.MessageId, out var id) ? id : null;

        if (!string.Equals(propriedades.Type, CobrancaPaga.Tipo, StringComparison.Ordinal))
        {
            return LeituraMensagem<NotificacaoCobranca>.Invalida($"Tipo de evento desconhecido: '{propriedades.Type}'.", idDaMensagem);
        }

        ConteudoLido? lido;
        try
        {
            lido = JsonSerializer.Deserialize<ConteudoLido>(corpo.Span, TransferenciaConcluidaV1.Json);
        }
        catch (JsonException)
        {
            // Sem a mensagem da exceção: ela pode repetir trechos do corpo.
            return LeituraMensagem<NotificacaoCobranca>.Invalida("Corpo inválido.", idDaMensagem);
        }

        if (lido is null)
        {
            return LeituraMensagem<NotificacaoCobranca>.Invalida("Corpo vazio.", idDaMensagem);
        }

        var versao = CabecalhosMensagem.LerInteiro(propriedades.Headers, CabecalhosMensagem.Versao) ?? lido.Versao;
        if (versao != CobrancaPaga.Versao)
        {
            return LeituraMensagem<NotificacaoCobranca>.Invalida($"Versão de schema não suportada: {versao?.ToString(CultureInfo.InvariantCulture) ?? "ausente"}.", lido.EventoId ?? idDaMensagem);
        }

        var ausentes = new List<string>();
        Exigir(lido.EventoId, "eventoId", ausentes);
        Exigir(lido.CobrancaId, "cobrancaId", ausentes);
        Exigir(lido.LojistaUsuarioId, "lojistaUsuarioId", ausentes);
        Exigir(lido.PagadorUsuarioId, "pagadorUsuarioId", ausentes);
        Exigir(lido.Valor, "valor", ausentes);
        Exigir(lido.OcorridoEm, "ocorridoEm", ausentes);
        if (ausentes.Count > 0)
        {
            return LeituraMensagem<NotificacaoCobranca>.Invalida($"Campos obrigatórios ausentes: {string.Join(", ", ausentes)}.", lido.EventoId ?? idDaMensagem);
        }

        return LeituraMensagem<NotificacaoCobranca>.Ok(
            new NotificacaoCobranca(
                EventoId.De(lido.EventoId!.Value),
                CobrancaId.De(lido.CobrancaId!.Value),
                UsuarioId.De(lido.LojistaUsuarioId!.Value),
                UsuarioId.De(lido.PagadorUsuarioId!.Value),
                lido.Valor!.Value,
                string.IsNullOrWhiteSpace(lido.LojistaNome) ? null : lido.LojistaNome),
            lido.EventoId.Value);
    }

    private static void Exigir<T>(T? valor, string campo, List<string> ausentes)
        where T : struct
    {
        if (valor is null || EqualityComparer<T>.Default.Equals(valor.Value, default))
        {
            ausentes.Add(campo);
        }
    }

    /// <summary>Forma de leitura: tudo opcional, para distinguir ausência de valor.</summary>
    private sealed record ConteudoLido(
        Guid? EventoId,
        int? Versao,
        Guid? CobrancaId,
        Guid? LojistaUsuarioId,
        Guid? PagadorUsuarioId,
        decimal? Valor,
        DateTimeOffset? OcorridoEm,
        string? LojistaNome);
}
