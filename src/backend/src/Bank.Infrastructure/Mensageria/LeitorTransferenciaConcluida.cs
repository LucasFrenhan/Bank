using System.Text.Json;
using Bank.Application.Abstracoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria.Contratos;
using Bank.SharedKernel;
using RabbitMQ.Client;

namespace Bank.Infrastructure.Mensageria;

/// <summary>Resultado da leitura de uma mensagem: válida (com o conteúdo) ou inválida (com o motivo).</summary>
internal sealed record LeituraMensagem<T>(T? Conteudo, Guid? EventoId, string? Erro)
    where T : class
{
    public bool Valida => Conteudo is not null;

    public static LeituraMensagem<T> Ok(T conteudo, Guid eventoId) => new(conteudo, eventoId, null);

    public static LeituraMensagem<T> Invalida(string erro, Guid? eventoId = null) => new(null, eventoId, erro);
}

/// <summary>
/// Lê o contrato <c>transferencia.concluida</c> v1 (specs/010-mensageria-rabbitmq, FR-011,
/// FR-018b; contracts/evento-transferencia-concluida.md) e o converte para os tipos do Domain.
/// Campos novos são opcionais (eventos antigos não os têm) e campos desconhecidos são ignorados;
/// falta de campo obrigatório, <c>type</c> diferente ou <c>versao</c> ≠ 1 tornam a mensagem inválida.
/// </summary>
internal static class LeitorTransferenciaConcluida
{
    public static LeituraMensagem<NotificacaoTransferencia> Ler(ReadOnlyMemory<byte> corpo, IReadOnlyBasicProperties propriedades)
    {
        ArgumentNullException.ThrowIfNull(propriedades);

        Guid? idDaMensagem = Guid.TryParse(propriedades.MessageId, out var id) ? id : null;

        if (!string.Equals(propriedades.Type, TransferenciaConcluida.Tipo, StringComparison.Ordinal))
        {
            return LeituraMensagem<NotificacaoTransferencia>.Invalida($"Tipo de evento desconhecido: '{propriedades.Type}'.", idDaMensagem);
        }

        ConteudoLido? lido;
        try
        {
            lido = JsonSerializer.Deserialize<ConteudoLido>(corpo.Span, TransferenciaConcluidaV1.Json);
        }
        catch (JsonException excecao)
        {
            return LeituraMensagem<NotificacaoTransferencia>.Invalida($"Corpo inválido: {excecao.Message}", idDaMensagem);
        }

        if (lido is null)
        {
            return LeituraMensagem<NotificacaoTransferencia>.Invalida("Corpo vazio.", idDaMensagem);
        }

        var versao = CabecalhosMensagem.LerInteiro(propriedades.Headers, CabecalhosMensagem.Versao) ?? lido.Versao;
        if (versao != TransferenciaConcluida.Versao)
        {
            return LeituraMensagem<NotificacaoTransferencia>.Invalida($"Versão de schema não suportada: {versao?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "ausente"}.", lido.EventoId ?? idDaMensagem);
        }

        var ausentes = new List<string>();
        Exigir(lido.EventoId, "eventoId", ausentes);
        Exigir(lido.TransferenciaId, "transferenciaId", ausentes);
        Exigir(lido.PagadorUsuarioId, "pagadorUsuarioId", ausentes);
        Exigir(lido.RecebedorUsuarioId, "recebedorUsuarioId", ausentes);
        Exigir(lido.Valor, "valor", ausentes);
        Exigir(lido.OcorridoEm, "ocorridoEm", ausentes);
        if (ausentes.Count > 0)
        {
            return LeituraMensagem<NotificacaoTransferencia>.Invalida($"Campos obrigatórios ausentes: {string.Join(", ", ausentes)}.", lido.EventoId ?? idDaMensagem);
        }

        return LeituraMensagem<NotificacaoTransferencia>.Ok(
            new NotificacaoTransferencia(
                EventoId.De(lido.EventoId!.Value),
                TransferenciaId.De(lido.TransferenciaId!.Value),
                UsuarioId.De(lido.PagadorUsuarioId!.Value),
                UsuarioId.De(lido.RecebedorUsuarioId!.Value),
                lido.Valor!.Value,
                lido.OcorridoEm!.Value,
                NomeOuNulo(lido.PagadorNome),
                NomeOuNulo(lido.RecebedorNome)),
            lido.EventoId.Value);
    }

    /// <summary>Nomes são opcionais (eventos antigos não os têm); em branco equivale a ausente (spec 011, R4).</summary>
    private static string? NomeOuNulo(string? nome) => string.IsNullOrWhiteSpace(nome) ? null : nome;

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
        Guid? TransferenciaId,
        Guid? PagadorUsuarioId,
        Guid? RecebedorUsuarioId,
        decimal? Valor,
        DateTimeOffset? OcorridoEm,
        string? PagadorNome,
        string? RecebedorNome);
}
