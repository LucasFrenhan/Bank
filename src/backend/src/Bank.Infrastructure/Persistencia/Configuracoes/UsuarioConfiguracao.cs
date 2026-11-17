using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>usuarios</c> (data-model.md).</summary>
internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public const string IndiceEmail = "ux_usuarios_email";
    public const string IndiceDocumento = "ux_usuarios_documento";

    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios", tabela => tabela.HasCheckConstraint(
            "ck_usuarios_tipo_conta",
            "tipo_conta IN ('PessoaFisica', 'PessoaJuridica')"));

        builder.HasKey(u => u.Id).HasName("pk_usuarios");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(Usuario.TamanhoMaximoNome).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(Email.TamanhoMaximo).IsRequired();
        builder.Property(u => u.Documento).HasColumnName("documento").HasMaxLength(Documento.TamanhoMaximo).IsRequired();
        builder.Property(u => u.TipoConta).HasColumnName("tipo_conta").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.SenhaHash).HasColumnName("senha_hash").HasColumnType("text").IsRequired();
        builder.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(IndiceEmail);
        builder.HasIndex(u => u.Documento).IsUnique().HasDatabaseName(IndiceDocumento);

        builder.HasOne(u => u.Carteira)
            .WithOne()
            .HasForeignKey<Domain.Carteiras.Carteira>(c => c.UsuarioId)
            .HasConstraintName("fk_carteiras_usuarios")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.Navigation(u => u.Carteira).IsRequired();
        builder.Ignore(u => u.LancamentoDeAbertura);
    }
}
