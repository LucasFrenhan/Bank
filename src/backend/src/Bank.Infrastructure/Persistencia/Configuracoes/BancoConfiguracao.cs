using Bank.Domain.Bancos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>bancos</c>: os cinco bancos fixos (specs/012-dados-bancarios-carteira, FR-001;
/// research R5), inseridos pela migration a partir de <see cref="Banco.Todos"/>.
/// </summary>
internal sealed class BancoConfiguracao : IEntityTypeConfiguration<Banco>
{
    public void Configure(EntityTypeBuilder<Banco> builder)
    {
        builder.ToTable("bancos");

        builder.HasKey(b => b.Codigo).HasName("pk_bancos");

        builder.Property(b => b.Codigo).HasColumnName("codigo").HasColumnType("char(3)").ValueGeneratedNever();
        builder.Property(b => b.Nome).HasColumnName("nome").HasMaxLength(Banco.TamanhoMaximoNome).IsRequired();

        builder.HasData(Banco.Todos.Select(b => new { b.Codigo, b.Nome }));
    }
}
