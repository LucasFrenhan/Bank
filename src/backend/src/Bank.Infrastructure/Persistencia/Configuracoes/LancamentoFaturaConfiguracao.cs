using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela imutável <c>lancamentos_fatura</c> (specs/014-cartao-credito-cobrancas, FR-026): cada compra é
/// lançada no máximo uma vez (FR-023).
/// </summary>
internal sealed class LancamentoFaturaConfiguracao : IEntityTypeConfiguration<LancamentoFatura>
{
    public void Configure(EntityTypeBuilder<LancamentoFatura> builder)
    {
        builder.ToTable("lancamentos_fatura", tabela => tabela.HasCheckConstraint("ck_lancamentos_fatura_valor_positivo", "valor > 0"));

        builder.HasKey(l => l.Id).HasName("pk_lancamentos_fatura");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.FaturaId).HasColumnName("fatura_id").IsRequired();
        builder.Property(l => l.CompraId).HasColumnName("compra_id").IsRequired();
        builder.Property(l => l.Estabelecimento).HasColumnName("estabelecimento").HasMaxLength(Usuario.TamanhoMaximoNome).IsRequired();
        builder.Property(l => l.Descricao).HasColumnName("descricao").HasMaxLength(Cobranca.TamanhoMaximoDescricao).IsRequired();
        builder.Property(l => l.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(l => l.DataCompra).HasColumnName("data_compra").IsRequired();
        builder.Property(l => l.LancadoEm).HasColumnName("lancado_em").IsRequired();

        builder.HasIndex(l => l.CompraId).IsUnique().HasDatabaseName("ux_lancamentos_fatura_compra_id");
        builder.HasIndex(l => l.FaturaId).HasDatabaseName("ix_lancamentos_fatura_fatura_id");

        builder.HasOne<Fatura>().WithMany().HasForeignKey(l => l.FaturaId)
            .HasConstraintName("fk_lancamentos_fatura_faturas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CompraCartao>().WithMany().HasForeignKey(l => l.CompraId)
            .HasConstraintName("fk_lancamentos_fatura_compras").OnDelete(DeleteBehavior.Restrict);
    }
}
