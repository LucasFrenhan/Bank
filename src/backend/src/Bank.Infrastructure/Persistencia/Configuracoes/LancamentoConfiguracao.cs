using Bank.Domain.Carteiras;
using Bank.Domain.Cartoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>lancamentos</c> — o razão das carteiras (specs/006-transferencias, data-model).</summary>
internal sealed class LancamentoConfiguracao : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("lancamentos", tabela =>
        {
            tabela.HasCheckConstraint("ck_lancamentos_valor_positivo", "valor > 0");
            tabela.HasCheckConstraint("ck_lancamentos_tipo", "tipo IN ('Abertura', 'Transferencia', 'PagamentoFatura', 'LiquidacaoRecebivel', 'AntecipacaoRecebivel')");
            tabela.HasCheckConstraint("ck_lancamentos_direcao", "direcao IN ('Entrada', 'Saida')");
            tabela.HasCheckConstraint("ck_lancamentos_origem_coerente",
                "(tipo = 'Transferencia') = (transferencia_id IS NOT NULL) AND (tipo = 'PagamentoFatura') = (fatura_id IS NOT NULL) AND (tipo IN ('LiquidacaoRecebivel', 'AntecipacaoRecebivel')) = (recebivel_id IS NOT NULL)");
        });

        builder.HasKey(l => l.Id).HasName("pk_lancamentos");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.CarteiraId).HasColumnName("carteira_id").IsRequired();
        builder.Property(l => l.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.Direcao).HasColumnName("direcao").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(l => l.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(l => l.TransferenciaId).HasColumnName("transferencia_id");
        builder.Property(l => l.FaturaId).HasColumnName("fatura_id");
        builder.Property(l => l.RecebivelId).HasColumnName("recebivel_id");
        builder.Property(l => l.Descricao).HasColumnName("descricao").HasMaxLength(DescricaoTransferencia.TamanhoMaximo);
        builder.Property(l => l.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(l => new { l.CarteiraId, l.CriadoEm, l.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_lancamentos_extrato");
        builder.HasIndex(l => l.CarteiraId)
            .IsUnique()
            .HasFilter("tipo = 'Abertura'")
            .HasDatabaseName("ux_lancamentos_abertura_por_carteira");
        builder.HasIndex(l => l.TransferenciaId).HasDatabaseName("ix_lancamentos_transferencia_id");

        // Uma fatura é paga e um recebível é creditado no máximo uma vez (specs/014-cartao-credito-cobrancas, R7).
        builder.HasIndex(l => l.FaturaId).IsUnique().HasFilter("fatura_id IS NOT NULL").HasDatabaseName("ux_lancamentos_fatura_id");
        builder.HasIndex(l => l.RecebivelId).IsUnique().HasFilter("recebivel_id IS NOT NULL").HasDatabaseName("ux_lancamentos_recebivel_id");

        builder.HasOne<Carteira>().WithMany().HasForeignKey(l => l.CarteiraId)
            .HasConstraintName("fk_lancamentos_carteiras").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Transferencia>().WithMany().HasForeignKey(l => l.TransferenciaId)
            .HasConstraintName("fk_lancamentos_transferencias").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fatura>().WithMany().HasForeignKey(l => l.FaturaId)
            .HasConstraintName("fk_lancamentos_faturas").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Recebivel>().WithMany().HasForeignKey(l => l.RecebivelId)
            .HasConstraintName("fk_lancamentos_recebiveis").OnDelete(DeleteBehavior.Restrict);
    }
}
