using Bank.Domain.Cartoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>bandeiras</c> (specs/014-cartao-credito-cobrancas, FR-001), com os registros de
/// <see cref="Bandeira.Todas"/> inseridos pela migration.
/// </summary>
internal sealed class BandeiraConfiguracao : IEntityTypeConfiguration<Bandeira>
{
    public void Configure(EntityTypeBuilder<Bandeira> builder)
    {
        builder.ToTable("bandeiras");

        builder.HasKey(b => b.Codigo).HasName("pk_bandeiras");

        builder.Property(b => b.Codigo).HasColumnName("codigo").HasColumnType("char(3)").ValueGeneratedNever();
        builder.Property(b => b.Nome).HasColumnName("nome").HasMaxLength(Bandeira.TamanhoMaximoNome).IsRequired();

        builder.HasData(Bandeira.Todas.Select(b => new { b.Codigo, b.Nome }));
    }
}
