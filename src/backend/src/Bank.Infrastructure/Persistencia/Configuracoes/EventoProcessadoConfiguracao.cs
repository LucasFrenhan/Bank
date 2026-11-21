using Bank.Infrastructure.Mensageria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>Tabela <c>eventos_processados</c> (specs/010-mensageria-rabbitmq, data-model, FR-019).</summary>
internal sealed class EventoProcessadoConfiguracao : IEntityTypeConfiguration<EventoProcessado>
{
    public const int TamanhoMaximoNomeConsumidor = 100;

    public void Configure(EntityTypeBuilder<EventoProcessado> builder)
    {
        builder.ToTable("eventos_processados");

        builder.HasKey(e => new { e.EventoId, e.NomeConsumidor }).HasName("pk_eventos_processados");

        builder.Property(e => e.EventoId).HasColumnName("evento_id").ValueGeneratedNever();
        builder.Property(e => e.NomeConsumidor).HasColumnName("nome_consumidor").HasMaxLength(TamanhoMaximoNomeConsumidor).IsRequired();
        builder.Property(e => e.ProcessadoEm).HasColumnName("processado_em").IsRequired();
    }
}
