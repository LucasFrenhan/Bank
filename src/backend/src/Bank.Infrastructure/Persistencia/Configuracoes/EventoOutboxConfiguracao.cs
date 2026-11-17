using Bank.Infrastructure.Mensageria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>outbox_eventos</c> (specs/006-transferencias, data-model; research R9).</summary>
internal sealed class EventoOutboxConfiguracao : IEntityTypeConfiguration<EventoOutbox>
{
    public void Configure(EntityTypeBuilder<EventoOutbox> builder)
    {
        builder.ToTable("outbox_eventos");

        builder.HasKey(e => e.Id).HasName("pk_outbox_eventos");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Versao).HasColumnName("versao").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(e => e.PublicadoEm).HasColumnName("publicado_em");

        builder.HasIndex(e => e.OcorridoEm)
            .HasFilter("publicado_em IS NULL")
            .HasDatabaseName("ix_outbox_eventos_pendentes");
    }
}
