using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>sessoes</c> (specs/016-sessao-refresh-token, data-model): uma por login; a exclusão do usuário apaga as
/// sessões e as credenciais.
/// </summary>
internal sealed class SessaoConfiguracao : IEntityTypeConfiguration<Sessao>
{
    public void Configure(EntityTypeBuilder<Sessao> builder)
    {
        builder.ToTable("sessoes", tabela =>
            tabela.HasCheckConstraint("ck_sessoes_revogacao", "(revogada_em IS NULL) = (motivo_revogacao IS NULL)"));

        builder.HasKey(s => s.Id).HasName("pk_sessoes");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(s => s.IniciadaEm).HasColumnName("iniciada_em").IsRequired();
        builder.Property(s => s.UltimaRenovacaoEm).HasColumnName("ultima_renovacao_em").IsRequired();
        builder.Property(s => s.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(s => s.RevogadaEm).HasColumnName("revogada_em");
        builder.Property(s => s.MotivoRevogacao).HasColumnName("motivo_revogacao").HasConversion<string>().HasMaxLength(20);
        builder.Ignore(s => s.Revogada);
        builder.Ignore(s => s.CredencialAtiva);

        builder.HasMany(s => s.Credenciais).WithOne().HasForeignKey(c => c.SessaoId)
            .HasConstraintName("fk_credenciais_renovacao_sessoes").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Credenciais).HasField("_credenciais").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.UsuarioId).HasDatabaseName("ix_sessoes_usuario_id");
        builder.HasIndex(s => s.ExpiraEm).HasDatabaseName("ix_sessoes_expira_em");
        builder.HasIndex(s => s.RevogadaEm).HasDatabaseName("ix_sessoes_revogada_em").HasFilter("revogada_em IS NOT NULL");

        builder.HasOne<Usuario>().WithMany().HasForeignKey(s => s.UsuarioId)
            .HasConstraintName("fk_sessoes_usuarios").OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Tabela <c>credenciais_renovacao</c>: só o SHA-256 da credencial (FR-004), único; no máximo uma ativa por sessão.
/// </summary>
internal sealed class CredencialRenovacaoConfiguracao : IEntityTypeConfiguration<CredencialRenovacao>
{
    public const string IndiceHash = "ux_credenciais_renovacao_hash";
    public const string IndiceAtiva = "ux_credenciais_renovacao_ativa";

    public void Configure(EntityTypeBuilder<CredencialRenovacao> builder)
    {
        builder.ToTable("credenciais_renovacao", tabela =>
            tabela.HasCheckConstraint("ck_credenciais_renovacao_hash", $"octet_length(hash) = {HashCredencial.Tamanho}"));

        builder.HasKey(c => c.Id).HasName("pk_credenciais_renovacao");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.SessaoId).HasColumnName("sessao_id").IsRequired();
        builder.Property(c => c.Hash).HasColumnName("hash").HasColumnType("bytea").IsRequired()
            .HasConversion(
                hash => hash.Bytes,
                bytes => HashCredencial.De(bytes),
                new ValueComparer<HashCredencial>((a, b) => a == b, h => h.GetHashCode(), h => h));
        builder.Property(c => c.EmitidaEm).HasColumnName("emitida_em").IsRequired();
        builder.Property(c => c.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(c => c.RotacionadaEm).HasColumnName("rotacionada_em");
        builder.Property(c => c.SubstitutaId).HasColumnName("substituta_id");
        builder.Ignore(c => c.Ativa);

        builder.HasIndex(c => c.Hash).IsUnique().HasDatabaseName(IndiceHash);
        builder.HasIndex(c => c.SessaoId).IsUnique().HasDatabaseName(IndiceAtiva).HasFilter("rotacionada_em IS NULL");
    }
}
