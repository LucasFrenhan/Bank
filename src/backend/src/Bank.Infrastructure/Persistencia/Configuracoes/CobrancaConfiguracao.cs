using Bank.Domain.Carteiras;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>cobrancas</c> (specs/014-cartao-credito-cobrancas, data-model; FR-009 a FR-013) e o código curto (specs/015, FR-009a).</summary>
internal sealed class CobrancaConfiguracao : IEntityTypeConfiguration<Cobranca>
{
    public const string IndiceCodigo = "ux_cobrancas_codigo";

    public void Configure(EntityTypeBuilder<Cobranca> builder)
    {
        builder.ToTable("cobrancas", tabela =>
        {
            tabela.HasCheckConstraint("ck_cobrancas_valor_positivo", "valor > 0");
            tabela.HasCheckConstraint("ck_cobrancas_codigo", "codigo ~ '^[2-9A-HJKMNP-TV-Z]{8}$'");
            tabela.HasCheckConstraint("ck_cobrancas_status", "status IN ('Pendente', 'Paga', 'Cancelada')");
            tabela.HasCheckConstraint("ck_cobrancas_paga_coerente",
                "num_nonnulls(pagador_usuario_id, codigo_bandeira, ultimos_digitos_cartao, codigo_autorizacao, paga_em) = CASE status WHEN 'Paga' THEN 5 ELSE 0 END");
            tabela.HasCheckConstraint("ck_cobrancas_cancelada_coerente", "(status = 'Cancelada') = (cancelada_em IS NOT NULL)");
            tabela.HasCheckConstraint("ck_cobrancas_recusa_coerente", "(ultima_recusa_motivo IS NULL) = (ultima_recusa_em IS NULL)");
            tabela.HasCheckConstraint("ck_cobrancas_recusa_motivo", "ultima_recusa_motivo IN ('CartaoNaoAutorizado', 'FaturaVencida', 'LimiteInsuficiente')");
            tabela.HasCheckConstraint("ck_cobrancas_ultimos_digitos", "ultimos_digitos_cartao ~ '^[0-9]{4}$'");
        });

        builder.HasKey(c => c.Id).HasName("pk_cobrancas");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.Codigo).HasColumnName("codigo").HasColumnType("char(8)").IsRequired()
            .HasConversion(c => c.Valor, valor => CodigoCobranca.Criar(valor)!);
        builder.Property(c => c.LojistaUsuarioId).HasColumnName("lojista_usuario_id").IsRequired();
        builder.Property(c => c.CarteiraDestinoId).HasColumnName("carteira_destino_id").IsRequired();
        builder.Property(c => c.Valor).HasColumnName("valor").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.Descricao).HasColumnName("descricao").HasMaxLength(Cobranca.TamanhoMaximoDescricao).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.PagadorUsuarioId).HasColumnName("pagador_usuario_id");
        builder.Property(c => c.CodigoBandeira).HasColumnName("codigo_bandeira").HasColumnType("char(3)");
        builder.Property(c => c.UltimosDigitosCartao).HasColumnName("ultimos_digitos_cartao").HasColumnType("char(4)");
        builder.Property(c => c.CodigoAutorizacao).HasColumnName("codigo_autorizacao").HasMaxLength(CodigoAutorizacao.Tamanho);
        builder.Property(c => c.PagaEm).HasColumnName("paga_em");
        builder.Property(c => c.UltimaRecusaMotivo).HasColumnName("ultima_recusa_motivo").HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.UltimaRecusaEm).HasColumnName("ultima_recusa_em");
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CanceladaEm).HasColumnName("cancelada_em");

        builder.HasIndex(c => new { c.LojistaUsuarioId, c.CriadoEm, c.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_cobrancas_lojista");
        builder.HasIndex(c => c.Codigo).IsUnique().HasDatabaseName(IndiceCodigo);
        builder.HasIndex(c => c.CarteiraDestinoId).HasDatabaseName("ix_cobrancas_carteira_destino_id");
        builder.HasIndex(c => c.PagadorUsuarioId).HasDatabaseName("ix_cobrancas_pagador_usuario_id");
        builder.HasIndex(c => c.CodigoBandeira).HasDatabaseName("ix_cobrancas_codigo_bandeira");

        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.LojistaUsuarioId)
            .HasConstraintName("fk_cobrancas_usuarios_lojista").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Carteira>().WithMany().HasForeignKey(c => c.CarteiraDestinoId)
            .HasConstraintName("fk_cobrancas_carteiras").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.PagadorUsuarioId)
            .HasConstraintName("fk_cobrancas_usuarios_pagador").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bandeira>().WithMany().HasForeignKey(c => c.CodigoBandeira)
            .HasConstraintName("fk_cobrancas_bandeiras").OnDelete(DeleteBehavior.Restrict);
    }
}
