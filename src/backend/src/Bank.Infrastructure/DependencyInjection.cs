using Bank.Application.Abstracoes;
using Bank.Infrastructure.Persistencia;
using Bank.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bank.Infrastructure;

/// <summary>Registro dos serviços de infraestrutura (persistência, segurança).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Persistência (research R7)
        services.AddOptions<PersistenciaOptions>().BindConfiguration(PersistenciaOptions.Secao);
        services.AddDbContext<BankDbContext>(opcoes =>
            opcoes.UseNpgsql(configuration.GetConnectionString(PersistenciaOptions.NomeConnectionString)));
        services.AddHostedService<MigracoesNaInicializacao>();
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<ICarteiraConsultas, CarteiraConsultas>();
        services.AddScoped<IUsuarioConsultas, UsuarioConsultas>();

        // Segurança (research R3, R4)
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, ValidadorJwtOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IHasherSenha, HasherSenhaIdentity>();
        services.AddSingleton<IEmissorToken, EmissorTokenJwt>();

        return services;
    }
}
