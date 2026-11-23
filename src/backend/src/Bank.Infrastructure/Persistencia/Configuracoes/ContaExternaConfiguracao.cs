using Bank.Domain.Bancos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>contas_externas</c>: contas dos bancos 002 a 005, só para registro
/// (specs/012-dados-bancarios-carteira, FR-011, FR-014; research R6).
/// </summary>
internal sealed class ContaExternaConfiguracao : IEntityTypeConfiguration<ContaExterna>
{
    public const string IndiceContaBancaria = "ux_contas_externas_conta_bancaria";
    public const string ChaveBanco = "fk_contas_externas_bancos_codigo_banco";
    public const string CheckCodigoBanco = "ck_contas_externas_codigo_banco";

    public void Configure(EntityTypeBuilder<ContaExterna> builder)
    {
        builder.ToTable("contas_externas", tabela =>
        {
            tabela.HasCheckConstraint(CheckCodigoBanco, $"codigo_banco <> '{Banco.CodigoOneBank}'");
            tabela.HasCheckConstraint("ck_contas_externas_tipo_conta_bancaria", ConversoresBancarios.CheckTipo);
            tabela.HasCheckConstraint("ck_contas_externas_formato_conta", ConversoresBancarios.CheckFormato);
            tabela.HasCheckConstraint("ck_contas_externas_digito_conta", ConversoresBancarios.CheckDigito);
            tabela.HasCheckConstraint("ck_contas_externas_titular_documento", "titular_documento ~ '^([0-9]{11}|[0-9]{14})$'");
        });

        builder.HasKey(c => c.Id).HasName("pk_contas_externas");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.CodigoBanco).HasColumnName("codigo_banco").HasColumnType("char(3)").IsRequired();
        builder.Property(c => c.Agencia).HasColumnName("agencia").HasColumnType("char(4)").IsRequired();
        builder.Property(c => c.NumeroConta).HasColumnName("numero_conta").HasColumnType("char(8)").IsRequired();
        builder.Property(c => c.DigitoConta).HasColumnName("digito_conta").HasColumnType("char(1)").IsRequired();
        builder.Property(c => c.TipoContaBancaria).HasColumnName("tipo_conta_bancaria")
            .HasConversion(ConversoresBancarios.Tipo).HasMaxLength(ConversoresBancarios.TamanhoMaximoTipo).IsRequired();
        builder.Property(c => c.TitularNome).HasColumnName("titular_nome").HasMaxLength(ContaExterna.TamanhoMaximoTitularNome).IsRequired();
        builder.Property(c => c.TitularDocumento).HasColumnName("titular_documento").HasMaxLength(ContaExterna.TamanhoMaximoTitularDocumento).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.Ignore(c => c.ContaBancaria);

        builder.HasIndex(c => new { c.CodigoBanco, c.Agencia, c.NumeroConta }).IsUnique().HasDatabaseName(IndiceContaBancaria);

        builder.HasOne<Banco>()
            .WithMany()
            .HasForeignKey(c => c.CodigoBanco)
            .HasConstraintName(ChaveBanco)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
