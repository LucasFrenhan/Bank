using Bank.Application.Abstracoes;
using Bank.Infrastructure.Mensageria;
using Bank.Infrastructure.Persistencia;
using Bank.Infrastructure.Processamento;
using Bank.Infrastructure.Seguranca;
using Bank.Infrastructure.Sessoes;
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
        services.AddSingleton<IGeradorCredencial, GeradorCredencialAleatoria>(); // spec 016, R1

        // Dados do cartão derivados, nunca gravados (specs/014-cartao-credito-cobrancas, research R1)
        services.AddOptions<ChaveCartoesOptions>().BindConfiguration(ChaveCartoesOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ChaveCartoesOptions>, ValidadorChaveCartoesOptions>();
        services.AddSingleton<IDadosCartao, DadosCartaoHmac>();

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

        // Cartão de crédito (specs/014-cartao-credito-cobrancas, research R9)
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddScoped<ICarteiraRepositorio, CarteiraRepositorio>();
        services.AddScoped<ICartaoRepositorio, CartaoRepositorio>();
        services.AddScoped<ICobrancaRepositorio, CobrancaRepositorio>();
        services.AddScoped<ICobrancaConsultas, CobrancaConsultas>();
        services.AddScoped<IFaturaRepositorio, FaturaRepositorio>();
        services.AddScoped<IFaturaConsultas, FaturaConsultas>();
        services.AddScoped<IRecebivelRepositorio, RecebivelRepositorio>();
        services.AddScoped<IRecebivelConsultas, RecebivelConsultas>();

        // Sessão renovável (specs/016-sessao-refresh-token, research R2, R8)
        services.AddScoped<SessaoRepositorio>();
        services.AddScoped<ISessaoRepositorio>(sp => sp.GetRequiredService<SessaoRepositorio>());
        services.AddScoped<ILimpezaSessoes>(sp => sp.GetRequiredService<SessaoRepositorio>());

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
        services.AddHostedService<ConsumidorNotificacoesCobranca>(); // spec 014, US7

        return services;
    }

    /// <summary>Agendamento da limpeza das sessões encerradas no Worker (specs/016-sessao-refresh-token, research R8).</summary>
    public static IServiceCollection AddLimpezaSessoes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<LimpezaSessoesOptions>().BindConfiguration(LimpezaSessoesOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<LimpezaSessoesOptions>, ValidadorLimpezaSessoesOptions>();
        services.AddHostedService<LimpezaSessoesHospedado>();

        return services;
    }

    /// <summary>Agendamento do processamento diário no Worker (specs/014-cartao-credito-cobrancas, research R7).</summary>
    public static IServiceCollection AddProcessamentoDiario(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ProcessamentoDiarioOptions>().BindConfiguration(ProcessamentoDiarioOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ProcessamentoDiarioOptions>, ValidadorProcessamentoDiarioOptions>();
        services.AddHostedService<ProcessamentoDiarioHospedado>();

        return services;
    }
}
