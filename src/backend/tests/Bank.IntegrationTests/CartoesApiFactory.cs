using Bank.Application;
using Bank.Application.Processamento;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

/// <summary>
/// API dos testes de cartão (specs/014-cartao-credito-cobrancas, research R17): relógio ajustável, saldo inicial PF
/// de R$ 5.000,00 e o processamento diário registrado, para chamá-lo sem o Worker.
/// </summary>
public class CartoesApiFactory : BankApiFactory
{
    public RelogioAjustavel Relogio { get; } = new();

    /// <summary>Uma rodada do processamento diário no horário do relógio.</summary>
    public async Task<ResumoProcessamento> ProcessarAsync()
    {
        await using var escopo = CriarEscopo();
        return await escopo.ServiceProvider.GetRequiredService<ProcessarDia>().ExecutarAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Ajusta o relógio e roda o processamento.</summary>
    public Task<ResumoProcessamento> ProcessarEmAsync(DateTimeOffset instante)
    {
        Relogio.Agora = instante;
        return ProcessarAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Carteiras:SaldoInicial:PessoaFisica"] = "5000.00",
            ["Transferencias:Limites:PessoaFisica:PorOperacao"] = "1000.00",
            ["Transferencias:Limites:PessoaFisica:Diario"] = "1000.00",
        }));
        builder.ConfigureTestServices(servicos =>
        {
            servicos.AddSingleton<TimeProvider>(Relogio);
            servicos.AddProcessamento();
        });
    }
}
