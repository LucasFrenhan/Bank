using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Bank.Domain.Comum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>carteiras</c> (data-model.md; research R7). Dados bancários obrigatórios, sempre no
/// banco 001, com dígito conferido pela function <c>calcular_digito_conta</c>
/// (specs/012-dados-bancarios-carteira, FR-002, FR-003, FR-014; research R4).
/// </summary>
internal sealed class CarteiraConfiguracao : IEntityTypeConfiguration<Carteira>
{
    public const string PropriedadeVersao = "Versao";
    public const string IndiceContaBancaria = "ux_carteiras_conta_bancaria";
    public const string ChaveBanco = "fk_carteiras_bancos_codigo_banco";
    public const string CheckCodigoBanco = "ck_carteiras_codigo_banco";
    public const string CheckDigitoConta = "ck_carteiras_digito_conta";

    public void Configure(EntityTypeBuilder<Carteira> builder)
    {
        builder.ToTable("carteiras", tabela =>
        {
            tabela.HasCheckConstraint("ck_carteiras_saldo_nao_negativo", "saldo >= 0");
            tabela.HasCheckConstraint("ck_carteiras_moeda", $"moeda = '{Dinheiro.MoedaPadrao}'");
            tabela.HasCheckConstraint(CheckCodigoBanco, $"codigo_banco = '{Banco.CodigoOneBank}'");
            tabela.HasCheckConstraint("ck_carteiras_tipo_conta_bancaria", ConversoresBancarios.CheckTipo);
            tabela.HasCheckConstraint("ck_carteiras_formato_conta", ConversoresBancarios.CheckFormato);
            tabela.HasCheckConstraint(CheckDigitoConta, ConversoresBancarios.CheckDigito);
        });

        builder.HasKey(c => c.Id).HasName("pk_carteiras");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(c => c.Saldo).HasColumnName("saldo").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.Moeda).HasColumnName("moeda").HasColumnType("char(3)")
            .HasDefaultValueSql($"'{Dinheiro.MoedaPadrao}'").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(c => c.CodigoBanco).HasColumnName("codigo_banco").HasColumnType("char(3)").IsRequired();
        builder.Property(c => c.Agencia).HasColumnName("agencia").HasColumnType("char(4)").IsRequired();
        builder.Property(c => c.NumeroConta).HasColumnName("numero_conta").HasColumnType("char(8)").IsRequired();
        builder.Property(c => c.DigitoConta).HasColumnName("digito_conta").HasColumnType("char(1)").IsRequired();
        builder.Property(c => c.TipoContaBancaria).HasColumnName("tipo_conta_bancaria")
            .HasConversion(ConversoresBancarios.Tipo).HasMaxLength(ConversoresBancarios.TamanhoMaximoTipo).IsRequired();
        builder.Ignore(c => c.ContaBancaria);

        // Concorrência otimista pela coluna de sistema xmin, para as movimentações (issue #8).
        builder.Property<uint>(PropriedadeVersao).IsRowVersion();

        builder.HasIndex(c => c.UsuarioId).IsUnique().HasDatabaseName("ux_carteiras_usuario_id");
        builder.HasIndex(c => new { c.CodigoBanco, c.Agencia, c.NumeroConta }).IsUnique().HasDatabaseName(IndiceContaBancaria);

        builder.HasOne<Banco>()
            .WithMany()
            .HasForeignKey(c => c.CodigoBanco)
            .HasConstraintName(ChaveBanco)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
