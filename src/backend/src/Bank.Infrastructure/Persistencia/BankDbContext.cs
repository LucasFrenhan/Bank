using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Contexto EF Core do PostgreSQL. O schema evolui só por migrations
/// (constituição, Restrições Técnicas; specs/003-usuarios-carteiras/data-model.md).
/// </summary>
public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Carteira> Carteiras => Set<Carteira>();

    public DbSet<Transferencia> Transferencias => Set<Transferencia>();

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    public DbSet<EventoOutbox> EventosOutbox => Set<EventoOutbox>();

    public DbSet<EventoProcessado> EventosProcessados => Set<EventoProcessado>();

    public DbSet<NotificacaoAuditoria> NotificacoesAuditoria => Set<NotificacaoAuditoria>();

    public DbSet<Banco> Bancos => Set<Banco>();

    public DbSet<ContaExterna> ContasExternas => Set<ContaExterna>();

    /// <summary>Sequence dos números de conta do banco 001 (specs/012-dados-bancarios-carteira, research R2).</summary>
    public const string SequenceNumeroConta = "seq_numero_conta_one_bank";

    /// <summary>Identificadores tipados do Domain gravados como <c>uuid</c> (issue #43).</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<UsuarioId>().HaveConversion<IdentificadorConverter<UsuarioId>>();
        configurationBuilder.Properties<CarteiraId>().HaveConversion<IdentificadorConverter<CarteiraId>>();
        configurationBuilder.Properties<TransferenciaId>().HaveConversion<IdentificadorConverter<TransferenciaId>>();
        configurationBuilder.Properties<LancamentoId>().HaveConversion<IdentificadorConverter<LancamentoId>>();
        configurationBuilder.Properties<EventoId>().HaveConversion<IdentificadorConverter<EventoId>>();
        configurationBuilder.Properties<NotificacaoAuditoriaId>().HaveConversion<IdentificadorConverter<NotificacaoAuditoriaId>>();
        configurationBuilder.Properties<ContaExternaId>().HaveConversion<IdentificadorConverter<ContaExternaId>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasSequence<long>(SequenceNumeroConta)
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMax(ContaBancaria.NumeroMaximo)
            .IsCyclic(false);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);
    }
}
