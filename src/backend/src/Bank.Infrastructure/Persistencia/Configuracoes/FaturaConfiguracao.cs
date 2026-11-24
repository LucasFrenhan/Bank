using Bank.Domain.Cartoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>faturas</c> (specs/014-cartao-credito-cobrancas, data-model; FR-024 a FR-028a).</summary>
internal sealed class FaturaConfiguracao : IEntityTypeConfiguration<Fatura>
{
    public const string IndiceChave = "ux_faturas_chave";

    public void Configure(EntityTypeBuilder<Fatura> builder)
    {
        builder.ToTable("faturas", tabela =>
        {
            tabela.HasCheckConstraint("ck_faturas_datas", "inicio < fechamento AND fechamento < vencimento AND EXTRACT(DAY FROM competencia) = 1");
            tabela.HasCheckConstraint("ck_faturas_total", "total >= 0");
            tabela.HasCheckConstraint("ck_faturas_pagamento_coerente",
                "num_nonnulls(paga_em, taxa_juros_mensal, dias_atraso, juros, valor_pago, chave_idempotencia) IN (0, 6) AND (paga_em IS NULL OR (liberada_em IS NOT NULL AND dias_atraso >= 0 AND juros >= 0 AND valor_pago = total + juros))");
        });

        builder.HasKey(f => f.Id).HasName("pk_faturas");

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.CartaoId).HasColumnName("cartao_id").IsRequired();
        builder.Property(f => f.Competencia).HasColumnName("competencia").IsRequired();
        builder.Property(f => f.Inicio).HasColumnName("inicio").IsRequired();
        builder.Property(f => f.Fechamento).HasColumnName("fechamento").IsRequired();
        builder.Property(f => f.Vencimento).HasColumnName("vencimento").IsRequired();
        builder.Property(f => f.Total).HasColumnName("total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(f => f.LiberadaEm).HasColumnName("liberada_em");
        builder.Property(f => f.PagaEm).HasColumnName("paga_em");
        builder.Property(f => f.TaxaJurosMensal).HasColumnName("taxa_juros_mensal").HasColumnType("numeric(5,2)");
        builder.Property(f => f.DiasAtraso).HasColumnName("dias_atraso");
        builder.Property(f => f.Juros).HasColumnName("juros").HasColumnType("numeric(18,2)");
        builder.Property(f => f.ValorPago).HasColumnName("valor_pago").HasColumnType("numeric(18,2)");
        builder.Property(f => f.ChaveIdempotencia).HasColumnName("chave_idempotencia");

        builder.HasIndex(f => new { f.CartaoId, f.Competencia }).IsUnique().HasDatabaseName("ux_faturas_cartao_competencia");
        builder.HasIndex(f => new { f.CartaoId, f.ChaveIdempotencia }).IsUnique().HasFilter("chave_idempotencia IS NOT NULL").HasDatabaseName(IndiceChave);
        builder.HasIndex(f => f.Fechamento).HasFilter("liberada_em IS NULL").HasDatabaseName("ix_faturas_nao_liberadas");
        builder.HasIndex(f => new { f.CartaoId, f.Vencimento }).HasFilter("liberada_em IS NOT NULL AND paga_em IS NULL").HasDatabaseName("ix_faturas_em_aberto");

        builder.HasOne<Cartao>().WithMany().HasForeignKey(f => f.CartaoId)
            .HasConstraintName("fk_faturas_cartoes").OnDelete(DeleteBehavior.Restrict);
    }
}
