using Bank.Domain.Carteiras;
using Bank.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>carteiras</c> (data-model.md; research R7).</summary>
internal sealed class CarteiraConfiguracao : IEntityTypeConfiguration<Carteira>
{
    public const string PropriedadeVersao = "Versao";

    public void Configure(EntityTypeBuilder<Carteira> builder)
    {
        builder.ToTable("carteiras", tabela =>
        {
            tabela.HasCheckConstraint("ck_carteiras_saldo_nao_negativo", "saldo >= 0");
            tabela.HasCheckConstraint("ck_carteiras_moeda", $"moeda = '{Dinheiro.MoedaPadrao}'");
        });

        builder.HasKey(c => c.Id).HasName("pk_carteiras");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(c => c.Saldo).HasColumnName("saldo").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.Moeda).HasColumnName("moeda").HasColumnType("char(3)")
            .HasDefaultValueSql($"'{Dinheiro.MoedaPadrao}'").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        // Concorrência otimista pela coluna de sistema xmin, para as movimentações (issue #8).
        builder.Property<uint>(PropriedadeVersao).IsRowVersion();

        builder.HasIndex(c => c.UsuarioId).IsUnique().HasDatabaseName("ux_carteiras_usuario_id");
    }
}
