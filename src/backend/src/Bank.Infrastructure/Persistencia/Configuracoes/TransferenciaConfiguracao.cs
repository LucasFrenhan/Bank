using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>transferencias</c> (specs/006-transferencias, data-model).</summary>
internal sealed class TransferenciaConfiguracao : IEntityTypeConfiguration<Transferencia>
{
    public const string IndiceChave = "ux_transferencias_origem_chave";

    public void Configure(EntityTypeBuilder<Transferencia> builder)
    {
        builder.ToTable("transferencias", tabela =>
        {
            tabela.HasCheckConstraint("ck_transferencias_valor_positivo", "valor > 0");
            tabela.HasCheckConstraint("ck_transferencias_origem_diferente_destino", "carteira_origem_id <> carteira_destino_id");
            tabela.HasCheckConstraint("ck_transferencias_status", "status = 'Concluida'");
        });

        builder.HasKey(t => t.Id).HasName("pk_transferencias");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.ChaveIdempotencia).HasColumnName("chave_idempotencia").IsRequired();
        builder.Property(t => t.CarteiraOrigemId).HasColumnName("carteira_origem_id").IsRequired();
        builder.Property(t => t.CarteiraDestinoId).HasColumnName("carteira_destino_id").IsRequired();
        builder.Property(t => t.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(t => t.Descricao).HasColumnName("descricao").HasMaxLength(DescricaoTransferencia.TamanhoMaximo);
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(t => new { t.CarteiraOrigemId, t.ChaveIdempotencia }).IsUnique().HasDatabaseName(IndiceChave);
        builder.HasIndex(t => t.CarteiraDestinoId).HasDatabaseName("ix_transferencias_carteira_destino_id");

        // Soma do dia por pagador sob bloqueio (specs/009-limite-transferencia, R3).
        builder.HasIndex(t => new { t.CarteiraOrigemId, t.CriadoEm }).HasDatabaseName("ix_transferencias_origem_criado_em");

        builder.HasOne<Carteira>().WithMany().HasForeignKey(t => t.CarteiraOrigemId)
            .HasConstraintName("fk_transferencias_carteira_origem").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Carteira>().WithMany().HasForeignKey(t => t.CarteiraDestinoId)
            .HasConstraintName("fk_transferencias_carteira_destino").OnDelete(DeleteBehavior.Restrict);
    }
}
