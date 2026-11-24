using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>notificacoes_auditoria</c> (specs/011-notificacoes-auditoria, data-model, FR-006,
/// FR-008; research R7). Tipo e canal gravados como texto <c>snake_case</c>.
/// </summary>
internal sealed class NotificacaoAuditoriaConfiguracao : IEntityTypeConfiguration<NotificacaoAuditoria>
{
    public const string IndiceEventoTipo = "ux_notificacoes_auditoria_evento_tipo";
    public const string ChaveUsuarioDestino = "fk_notificacoes_auditoria_usuarios_usuario_destino_id";

    private static readonly ValueConverter<TipoNotificacao, string> ConversorTipo = new(
        tipo => ParaTexto(tipo),
        texto => ParaTipo(texto));

    private static readonly ValueConverter<CanalNotificacao, string> ConversorCanal = new(
        canal => canal == CanalNotificacao.Simulado ? "simulado" : canal.ToString(),
        texto => texto == "simulado" ? CanalNotificacao.Simulado : Enum.Parse<CanalNotificacao>(texto));

    public void Configure(EntityTypeBuilder<NotificacaoAuditoria> builder)
    {
        builder.ToTable("notificacoes_auditoria", tabela =>
        {
            tabela.HasCheckConstraint("ck_notificacoes_auditoria_tipo", "tipo IN ('comprovante_envio', 'aviso_credito', 'comprovante_cobranca', 'cobranca_paga')");
            tabela.HasCheckConstraint("ck_notificacoes_auditoria_canal", "canal IN ('simulado')");
        });

        builder.HasKey(n => n.Id).HasName("pk_notificacoes_auditoria");

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(n => n.EventoId).HasColumnName("evento_id").IsRequired();
        builder.Property(n => n.UsuarioDestinoId).HasColumnName("usuario_destino_id").IsRequired();
        builder.Property(n => n.Tipo).HasColumnName("tipo").HasConversion(ConversorTipo).HasMaxLength(NotificacaoAuditoria.TamanhoMaximoTipo).IsRequired();
        builder.Property(n => n.Canal).HasColumnName("canal").HasConversion(ConversorCanal).HasMaxLength(NotificacaoAuditoria.TamanhoMaximoCanal).IsRequired();
        builder.Property(n => n.Mensagem).HasColumnName("mensagem").HasMaxLength(NotificacaoAuditoria.TamanhoMaximoMensagem).IsRequired();
        builder.Property(n => n.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(n => new { n.EventoId, n.Tipo }).IsUnique().HasDatabaseName(IndiceEventoTipo);
        builder.HasIndex(n => n.UsuarioDestinoId).HasDatabaseName("ix_notificacoes_auditoria_usuario_destino_id");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(n => n.UsuarioDestinoId)
            .HasConstraintName(ChaveUsuarioDestino)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }

    private static string ParaTexto(TipoNotificacao tipo) => tipo switch
    {
        TipoNotificacao.ComprovanteEnvio => "comprovante_envio",
        TipoNotificacao.AvisoCredito => "aviso_credito",
        TipoNotificacao.ComprovanteCobranca => "comprovante_cobranca",
        TipoNotificacao.CobrancaPaga => "cobranca_paga",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de notificação sem texto definido."),
    };

    private static TipoNotificacao ParaTipo(string texto) => texto switch
    {
        "comprovante_envio" => TipoNotificacao.ComprovanteEnvio,
        "aviso_credito" => TipoNotificacao.AvisoCredito,
        "comprovante_cobranca" => TipoNotificacao.ComprovanteCobranca,
        "cobranca_paga" => TipoNotificacao.CobrancaPaga,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, "Tipo de notificação desconhecido."),
    };
}
