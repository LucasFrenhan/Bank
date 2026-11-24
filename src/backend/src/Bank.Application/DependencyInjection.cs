using Bank.Application.Autenticacao;
using Bank.Application.Carteiras;
using Bank.Application.Cartoes;
using Bank.Application.Cobrancas;
using Bank.Application.Abstracoes;
using Bank.Application.Faturas;
using Bank.Application.Notificacoes;
using Bank.Application.Observabilidade;
using Bank.Application.Processamento;
using Bank.Application.Recebiveis;
using Bank.Application.Usuarios;
using Bank.Application.Transferencias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bank.Application;

/// <summary>Registro dos serviços da camada de aplicação.</summary>
public static class DependencyInjection
{
    /// <param name="ambienteDeProducao">
    /// Em produção, o saldo inicial de demonstração deve ser zero (FR-013; research R8).
    /// </param>
    public static IServiceCollection AddApplication(this IServiceCollection services, bool ambienteDeProducao)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SaldoInicialOptions>()
            .BindConfiguration(SaldoInicialOptions.Secao)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SaldoInicialOptions>>(new ValidadorSaldoInicialOptions(ambienteDeProducao));

        services.AddOptions<LimitesTransferenciaOptions>()
            .BindConfiguration(LimitesTransferenciaOptions.Secao)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LimitesTransferenciaOptions>, ValidadorLimitesTransferenciaOptions>();

        services.AddOptions<CartoesOptions>()
            .BindConfiguration(CartoesOptions.Secao)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CartoesOptions>, ValidadorCartoesOptions>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<CalendarioBrasilia>();
        services.AddSingleton<MetricasCartoes>();
        services.AddSingleton<MetricasUsuarios>();
        services.AddSingleton<HashSenhaFicticio>();

        services.AddScoped<RegistrarUsuario>();
        services.AddScoped<Autenticar>();
        services.AddScoped<ObterMinhaCarteira>();
        services.AddScoped<ObterTitular>();
        services.AddSingleton<MetricasTransferencias>();
        services.AddScoped<RealizarTransferencia>();
        services.AddScoped<ConsultarDestinatario>();
        services.AddScoped<ObterExtrato>();
        services.AddScoped<ConsultarTransferencia>();
        services.AddScoped<ConsultarLimites>();

        // Cartão de crédito (specs/014-cartao-credito-cobrancas)
        services.AddScoped<SolicitarCartao>();
        services.AddScoped<ObterMeuCartao>();
        services.AddScoped<ExibirDadosCartao>();
        services.AddScoped<EmitirCobranca>();
        services.AddScoped<ConsultarCobranca>();
        services.AddScoped<ListarMinhasCobrancas>();
        services.AddScoped<CancelarCobranca>();
        services.AddScoped<PagarCobranca>();
        services.AddScoped<ListarFaturas>();
        services.AddScoped<ObterFatura>();
        services.AddScoped<PagarFatura>();
        services.AddScoped<ListarRecebiveis>();
        services.AddScoped<ObterRecebivel>();
        services.AddScoped<AnteciparRecebivel>();

        return services;
    }

    /// <summary>
    /// Casos de uso do Worker (specs/010-mensageria-rabbitmq, research R8): só as notificações. O
    /// Worker não registra <see cref="AddApplication"/>, cujos casos de uso dependem de serviços
    /// que só a API tem (hash de senha, emissão de token).
    /// </summary>
    public static IServiceCollection AddNotificacoes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MetricasNotificacoes>();
        services.AddScoped<INotificadorTransferencia, NotificarTransferenciaConcluida>();
        services.AddScoped<INotificadorCobranca, NotificarCobrancaPaga>();

        return services;
    }

    /// <summary>Processamento diário, hospedado pelo Worker (specs/014-cartao-credito-cobrancas, research R7).</summary>
    public static IServiceCollection AddProcessamento(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<CalendarioBrasilia>();
        services.AddSingleton<MetricasProcessamento>();
        services.AddScoped<ProcessarDia>();

        return services;
    }
}
