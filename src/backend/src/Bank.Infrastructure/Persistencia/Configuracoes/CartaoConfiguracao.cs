using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Tabela <c>cartoes</c> (specs/014-cartao-credito-cobrancas, data-model): um por usuário, sem número nem
/// CVV (FR-003, FR-008); o limite utilizado nunca passa do total (FR-020).
/// </summary>
internal sealed class CartaoConfiguracao : IEntityTypeConfiguration<Cartao>
{
    public const string IndiceUsuario = "ux_cartoes_usuario_id";
    public const string IndiceImpressaoDigital = "ux_cartoes_impressao_digital";

    public void Configure(EntityTypeBuilder<Cartao> builder)
    {
        builder.ToTable("cartoes", tabela =>
        {
            tabela.HasCheckConstraint("ck_cartoes_limite", "limite_total > 0 AND limite_utilizado BETWEEN 0 AND limite_total");
            tabela.HasCheckConstraint("ck_cartoes_ultimos_digitos", "ultimos_digitos ~ '^[0-9]{4}$'");
            tabela.HasCheckConstraint("ck_cartoes_validade", "validade_mes BETWEEN 1 AND 12");
            tabela.HasCheckConstraint("ck_cartoes_variacao", $"variacao BETWEEN 0 AND {Cartao.MaximoVariacao}");
        });

        builder.HasKey(c => c.Id).HasName("pk_cartoes");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(c => c.CodigoBandeira).HasColumnName("codigo_bandeira").HasColumnType("char(3)").IsRequired();
        builder.Property(c => c.NomeImpresso).HasColumnName("nome_impresso").HasMaxLength(Usuario.TamanhoMaximoNome).IsRequired();
        builder.Property(c => c.UltimosDigitos).HasColumnName("ultimos_digitos").HasColumnType("char(4)").IsRequired();
        builder.Property(c => c.ValidadeAno).HasColumnName("validade_ano").IsRequired();
        builder.Property(c => c.ValidadeMes).HasColumnName("validade_mes").IsRequired();
        builder.Property(c => c.Variacao).HasColumnName("variacao").IsRequired();
        builder.Property(c => c.ImpressaoDigital).HasColumnName("impressao_digital").HasMaxLength(44).IsRequired();
        builder.Property(c => c.LimiteTotal).HasColumnName("limite_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.LimiteUtilizado).HasColumnName("limite_utilizado").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Ignore(c => c.LimiteDisponivel);
        builder.Ignore(c => c.Validade);

        builder.HasIndex(c => c.UsuarioId).IsUnique().HasDatabaseName(IndiceUsuario);
        builder.HasIndex(c => c.ImpressaoDigital).IsUnique().HasDatabaseName(IndiceImpressaoDigital);
        builder.HasIndex(c => c.CodigoBandeira).HasDatabaseName("ix_cartoes_codigo_bandeira");

        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.UsuarioId)
            .HasConstraintName("fk_cartoes_usuarios").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bandeira>().WithMany().HasForeignKey(c => c.CodigoBandeira)
            .HasConstraintName("fk_cartoes_bandeiras").OnDelete(DeleteBehavior.Restrict);
    }
}
