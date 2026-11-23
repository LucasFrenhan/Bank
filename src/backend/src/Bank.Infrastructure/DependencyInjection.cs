using Bank.Application.Abstracoes;
using Bank.Infrastructure.Mensageria;
using Bank.Infrastructure.Persistencia;
using Bank.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure;

/// <summary>Registro dos serviços de infraestrutura (persistência, segurança, mensageria).</summary>
public static class DependencyInjection
{
    /// <summary>Tudo o que a API usa: persistência, serviços de inicialização do banco e segurança.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPersistencia(configuration);

        // Só a API aplica migrations e lançamentos de abertura (specs/010-mensageria-rabbitmq, R9).
        services.AddHostedService<MigracoesNaInicializacao>();
        services.AddHostedService<LancamentosDeAberturaNaInicializacao>(); // depois das migrations

        // Segurança (research R3, R4)
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, ValidadorJwtOptions>();
        services.AddSingleton<IHasherSenha, HasherSenhaIdentity>();
        services.AddSingleton<IEmissorToken, EmissorTokenJwt>();

        return services;
    }

    /// <summary>Persistência compartilhada pela API e pelo Worker (research R7; spec 010, R9).</summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PersistenciaOptions>().BindConfiguration(PersistenciaOptions.Secao);
        services.AddDbContext<BankDbContext>(opcoes =>
            opcoes.UseNpgsql(configuration.GetConnectionString(PersistenciaOptions.NomeConnectionString)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IGeradorNumeroConta, GeradorNumeroConta>(); // spec 012, R2
        services.AddScoped<ICarteiraConsultas, CarteiraConsultas>();
        services.AddScoped<IUsuarioConsultas, UsuarioConsultas>();
        services.AddScoped<ITransferenciaRepositorio, TransferenciaRepositorio>();
        services.AddScoped<IUnidadeDeTransferencia, UnidadeDeTransferencia>();
        services.AddScoped<IExtratoConsultas, ExtratoConsultas>();
        services.AddScoped<INotificacoesAuditoria, NotificacoesAuditoriaRepositorio>(); // spec 011, R2

        return services;
    }

    /// <summary>
    /// Mensageria do Worker: conexão, topologia, despachante do outbox e consumidores
    /// (specs/010-mensageria-rabbitmq, research R1, R3–R7).
    /// </summary>
    public static IServiceCollection AddMensageria(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MensageriaOptions>().BindConfiguration(MensageriaOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<MensageriaOptions>, ValidadorMensageriaOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.AddSingleton<TelemetriaMensageria>();
        services.AddSingleton<ConexaoRabbitMq>();
        services.AddSingleton<EstadoMensageria>();
        services.AddHostedService<TopologiaNaInicializacao>();
        services.AddHostedService<DespachanteOutbox>();
        services.AddHostedService<ConsumidorNotificacoesTransferencia>();

        return services;
    }
}
