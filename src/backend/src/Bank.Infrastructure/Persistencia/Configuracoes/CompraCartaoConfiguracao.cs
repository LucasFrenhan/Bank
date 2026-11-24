using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela imutável <c>compras_cartao</c> (specs/014-cartao-credito-cobrancas, FR-018; research R5).</summary>
internal sealed class CompraCartaoConfiguracao : IEntityTypeConfiguration<CompraCartao>
{
    public void Configure(EntityTypeBuilder<CompraCartao> builder)
    {
        builder.ToTable("compras_cartao", tabela => tabela.HasCheckConstraint("ck_compras_cartao_valor_positivo", "valor > 0"));

        builder.HasKey(c => c.Id).HasName("pk_compras_cartao");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.CartaoId).HasColumnName("cartao_id").IsRequired();
        builder.Property(c => c.CobrancaId).HasColumnName("cobranca_id").IsRequired();
        builder.Property(c => c.AutorizacaoId).HasColumnName("autorizacao_id").IsRequired();
        builder.Property(c => c.FaturaId).HasColumnName("fatura_id").IsRequired();
        builder.Property(c => c.Estabelecimento).HasColumnName("estabelecimento").HasMaxLength(Usuario.TamanhoMaximoNome).IsRequired();
        builder.Property(c => c.Descricao).HasColumnName("descricao").HasMaxLength(Cobranca.TamanhoMaximoDescricao).IsRequired();
        builder.Property(c => c.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.DataCompra).HasColumnName("data_compra").IsRequired();
        builder.Property(c => c.AutorizadaEm).HasColumnName("autorizada_em").IsRequired();

        builder.HasIndex(c => c.CobrancaId).IsUnique().HasDatabaseName("ux_compras_cartao_cobranca_id");
        builder.HasIndex(c => c.AutorizacaoId).IsUnique().HasDatabaseName("ux_compras_cartao_autorizacao_id");
        builder.HasIndex(c => c.FaturaId).HasDatabaseName("ix_compras_cartao_fatura_id");
        builder.HasIndex(c => c.CartaoId).HasDatabaseName("ix_compras_cartao_cartao_id");

        builder.HasOne<Cartao>().WithMany().HasForeignKey(c => c.CartaoId)
            .HasConstraintName("fk_compras_cartao_cartoes").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Cobranca>().WithMany().HasForeignKey(c => c.CobrancaId)
            .HasConstraintName("fk_compras_cartao_cobrancas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AutorizacaoCartao>().WithMany().HasForeignKey(c => c.AutorizacaoId)
            .HasConstraintName("fk_compras_cartao_autorizacoes").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fatura>().WithMany().HasForeignKey(c => c.FaturaId)
            .HasConstraintName("fk_compras_cartao_faturas").OnDelete(DeleteBehavior.Restrict);
    }
}
