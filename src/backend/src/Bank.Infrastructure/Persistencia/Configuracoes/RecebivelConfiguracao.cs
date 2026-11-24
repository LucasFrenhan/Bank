using Bank.Domain.Carteiras;
using Bank.Domain.Cobrancas;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>recebiveis</c> (specs/014-cartao-credito-cobrancas, data-model; FR-030 a FR-033).</summary>
internal sealed class RecebivelConfiguracao : IEntityTypeConfiguration<Recebivel>
{
    public const string IndiceChave = "ux_recebiveis_chave";

    public void Configure(EntityTypeBuilder<Recebivel> builder)
    {
        builder.ToTable("recebiveis", tabela =>
        {
            tabela.HasCheckConstraint("ck_recebiveis_valores", "valor_bruto > 0 AND taxa >= 0 AND valor_liquido > 0 AND valor_liquido = valor_bruto - taxa");
            tabela.HasCheckConstraint("ck_recebiveis_situacao", "situacao IN ('Pendente', 'Liquidado', 'Antecipado')");
            tabela.HasCheckConstraint("ck_recebiveis_situacao_coerente",
                "(situacao = 'Pendente') = (recebido_em IS NULL) AND (situacao = 'Antecipado') = (chave_idempotencia IS NOT NULL) AND (situacao = 'Antecipado' OR taxa = 0)");
        });

        builder.HasKey(r => r.Id).HasName("pk_recebiveis");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.CobrancaId).HasColumnName("cobranca_id").IsRequired();
        builder.Property(r => r.LojistaUsuarioId).HasColumnName("lojista_usuario_id").IsRequired();
        builder.Property(r => r.CarteiraId).HasColumnName("carteira_id").IsRequired();
        builder.Property(r => r.ValorBruto).HasColumnName("valor_bruto").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(r => r.Taxa).HasColumnName("taxa").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(r => r.ValorLiquido).HasColumnName("valor_liquido").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(r => r.DataPrevista).HasColumnName("data_prevista").IsRequired();
        builder.Property(r => r.Situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(r => r.RecebidoEm).HasColumnName("recebido_em");
        builder.Property(r => r.ChaveIdempotencia).HasColumnName("chave_idempotencia");

        builder.HasIndex(r => r.CobrancaId).IsUnique().HasDatabaseName("ux_recebiveis_cobranca_id");
        builder.HasIndex(r => new { r.LojistaUsuarioId, r.ChaveIdempotencia }).IsUnique().HasFilter("chave_idempotencia IS NOT NULL").HasDatabaseName(IndiceChave);
        builder.HasIndex(r => r.DataPrevista).HasFilter("situacao = 'Pendente'").HasDatabaseName("ix_recebiveis_devidos");
        builder.HasIndex(r => new { r.LojistaUsuarioId, r.CriadoEm, r.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_recebiveis_lojista");
        builder.HasIndex(r => r.CarteiraId).HasDatabaseName("ix_recebiveis_carteira_id");

        builder.HasOne<Cobranca>().WithMany().HasForeignKey(r => r.CobrancaId)
            .HasConstraintName("fk_recebiveis_cobrancas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.LojistaUsuarioId)
            .HasConstraintName("fk_recebiveis_usuarios").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Carteira>().WithMany().HasForeignKey(r => r.CarteiraId)
            .HasConstraintName("fk_recebiveis_carteiras").OnDelete(DeleteBehavior.Restrict);
    }
}
