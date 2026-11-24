using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela imutável <c>autorizacoes_cartao</c> (specs/014-cartao-credito-cobrancas, FR-017): a chave de
/// idempotência é única por pagador só nas aprovações (FR-019; research R10).
/// </summary>
internal sealed class AutorizacaoCartaoConfiguracao : IEntityTypeConfiguration<AutorizacaoCartao>
{
    public const string IndiceChave = "ux_autorizacoes_cartao_chave";

    private const string SoAprovadas = "resultado = 'Aprovada'";

    public void Configure(EntityTypeBuilder<AutorizacaoCartao> builder)
    {
        builder.ToTable("autorizacoes_cartao", tabela =>
        {
            tabela.HasCheckConstraint("ck_autorizacoes_cartao_valor_positivo", "valor > 0");
            tabela.HasCheckConstraint("ck_autorizacoes_cartao_resultado", "resultado IN ('Aprovada', 'Recusada')");
            tabela.HasCheckConstraint("ck_autorizacoes_cartao_motivo", "motivo IN ('CartaoNaoAutorizado', 'FaturaVencida', 'LimiteInsuficiente')");
            tabela.HasCheckConstraint("ck_autorizacoes_cartao_resultado_coerente",
                "(resultado = 'Aprovada') = (codigo_autorizacao IS NOT NULL) AND (resultado = 'Aprovada') = (motivo IS NULL) AND (resultado = 'Recusada' OR (cartao_id IS NOT NULL AND codigo_bandeira IS NOT NULL))");
            tabela.HasCheckConstraint("ck_autorizacoes_cartao_ultimos_digitos", "ultimos_digitos ~ '^[0-9]{4}$'");
        });

        builder.HasKey(a => a.Id).HasName("pk_autorizacoes_cartao");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.CobrancaId).HasColumnName("cobranca_id").IsRequired();
        builder.Property(a => a.PagadorUsuarioId).HasColumnName("pagador_usuario_id").IsRequired();
        builder.Property(a => a.CartaoId).HasColumnName("cartao_id");
        builder.Property(a => a.CodigoBandeira).HasColumnName("codigo_bandeira").HasColumnType("char(3)");
        builder.Property(a => a.UltimosDigitos).HasColumnName("ultimos_digitos").HasColumnType("char(4)").IsRequired();
        builder.Property(a => a.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(a => a.Resultado).HasColumnName("resultado").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(a => a.Motivo).HasColumnName("motivo").HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.CodigoAutorizacao).HasColumnName("codigo_autorizacao").HasMaxLength(CodigoAutorizacao.Tamanho);
        builder.Property(a => a.ChaveIdempotencia).HasColumnName("chave_idempotencia").IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(a => new { a.PagadorUsuarioId, a.ChaveIdempotencia }).IsUnique().HasFilter(SoAprovadas).HasDatabaseName(IndiceChave);
        builder.HasIndex(a => a.CobrancaId).IsUnique().HasFilter(SoAprovadas).HasDatabaseName("ux_autorizacoes_cartao_cobranca_aprovada");
        builder.HasIndex(a => a.CartaoId).HasDatabaseName("ix_autorizacoes_cartao_cartao_id");
        builder.HasIndex(a => a.CodigoBandeira).HasDatabaseName("ix_autorizacoes_cartao_codigo_bandeira");

        builder.HasOne<Cobranca>().WithMany().HasForeignKey(a => a.CobrancaId)
            .HasConstraintName("fk_autorizacoes_cartao_cobrancas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(a => a.PagadorUsuarioId)
            .HasConstraintName("fk_autorizacoes_cartao_usuarios").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Cartao>().WithMany().HasForeignKey(a => a.CartaoId)
            .HasConstraintName("fk_autorizacoes_cartao_cartoes").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bandeira>().WithMany().HasForeignKey(a => a.CodigoBandeira)
            .HasConstraintName("fk_autorizacoes_cartao_bandeiras").OnDelete(DeleteBehavior.Restrict);
    }
}
