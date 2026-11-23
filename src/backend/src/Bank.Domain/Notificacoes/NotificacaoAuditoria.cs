using System.Globalization;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Notificacoes;

/// <summary>
/// Registro imutável de uma notificação de transferência gerada para um titular
/// (specs/011-notificacoes-auditoria, FR-001–FR-004, FR-007; contracts/notificacoes-auditoria.md).
/// Cada evento <c>transferencia.concluida</c> gera duas: o comprovante de envio (pagador) e o aviso
/// de crédito (recebedor). O texto leva o valor e, quando o evento traz, o nome da outra parte;
/// nunca a descrição da transferência.
/// </summary>
public sealed class NotificacaoAuditoria
{
    public const int TamanhoMaximoMensagem = 500;
    public const int TamanhoMaximoTipo = 30;
    public const int TamanhoMaximoCanal = 30;

    /// <summary>
    /// Formato brasileiro (<c>1.234,56</c>) sem depender da cultura do processo: a solução roda com
    /// <c>InvariantGlobalization</c>, sem a cultura pt-BR (research R5).
    /// </summary>
    private static readonly NumberFormatInfo FormatoReais = CriarFormatoReais();

    private NotificacaoAuditoria()
    {
        Mensagem = string.Empty;
    }

    private NotificacaoAuditoria(EventoId eventoId, UsuarioId usuarioDestinoId, TipoNotificacao tipo, string mensagem, DateTimeOffset agora)
    {
        Id = NotificacaoAuditoriaId.Novo(agora);
        EventoId = eventoId;
        UsuarioDestinoId = usuarioDestinoId;
        Tipo = tipo;
        Canal = CanalNotificacao.Simulado;
        Mensagem = mensagem;
        CriadoEm = agora;
    }

    public NotificacaoAuditoriaId Id { get; private set; }

    public EventoId EventoId { get; private set; }

    public UsuarioId UsuarioDestinoId { get; private set; }

    public TipoNotificacao Tipo { get; private set; }

    public CanalNotificacao Canal { get; private set; }

    public string Mensagem { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>Comprovante de envio para o pagador: "Você enviou R$ {valor} para {recebedor}."</summary>
    public static NotificacaoAuditoria ComprovanteEnvio(EventoId eventoId, UsuarioId pagador, decimal valor, string? recebedorNome, DateTimeOffset agora) =>
        new(eventoId, pagador, TipoNotificacao.ComprovanteEnvio, Texto("Você enviou", valor, "para", recebedorNome), agora);

    /// <summary>Aviso de crédito para o recebedor: "Você recebeu R$ {valor} de {pagador}."</summary>
    public static NotificacaoAuditoria AvisoCredito(EventoId eventoId, UsuarioId recebedor, decimal valor, string? pagadorNome, DateTimeOffset agora) =>
        new(eventoId, recebedor, TipoNotificacao.AvisoCredito, Texto("Você recebeu", valor, "de", pagadorNome), agora);

    private static string Texto(string acao, decimal valor, string preposicao, string? nomeOutraParte)
    {
        var valorFormatado = valor.ToString("#,##0.00", FormatoReais);
        var nome = nomeOutraParte?.Trim();
        return string.IsNullOrEmpty(nome)
            ? $"{acao} R$ {valorFormatado}."
            : $"{acao} R$ {valorFormatado} {preposicao} {nome}.";
    }

    private static NumberFormatInfo CriarFormatoReais()
    {
        var formato = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        formato.NumberDecimalSeparator = ",";
        formato.NumberGroupSeparator = ".";
        return NumberFormatInfo.ReadOnly(formato);
    }
}
