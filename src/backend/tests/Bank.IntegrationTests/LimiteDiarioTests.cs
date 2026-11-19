using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Relógio do sistema até ser ajustado: contas e tokens nascem no horário real (a validação do JWT
/// usa o relógio do sistema) e só as transferências usam o horário controlado.
/// </summary>
public sealed class RelogioAjustavel : TimeProvider
{
    public DateTimeOffset? Agora { get; set; }

    public override DateTimeOffset GetUtcNow() => Agora ?? System.GetUtcNow();
}

/// <summary>
/// Fábrica do limite diário (spec 009, US2): saldo inicial PF de R$ 5.000,00, diário de
/// R$ 2.000,00 e limite por operação vazio (desligado), para que só o diário recuse.
/// </summary>
public sealed class LimiteDiarioApiFactory : BankApiFactory
{
    public RelogioAjustavel Relogio { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Carteiras:SaldoInicial:PessoaFisica"] = "5000.00",
            ["Transferencias:Limites:PessoaFisica:PorOperacao"] = string.Empty,
            ["Transferencias:Limites:PessoaFisica:Diario"] = "2000.00",
        }));
        builder.ConfigureTestServices(servicos => servicos.AddSingleton<TimeProvider>(Relogio));
    }
}

/// <summary>Limite diário no dia de Brasília (specs/009-limite-transferencia, US2; FR-001 a FR-003).</summary>
public class LimiteDiarioTests(LimiteDiarioApiFactory factory) : IClassFixture<LimiteDiarioApiFactory>, IDisposable
{
    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Sequencia_1500_600Recusada_500_0e01Recusada()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using (var primeira = await TransferirAsync(client, pf, pj.Documento, 1500.00m))
        {
            Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        }

        using (var recusada = await TransferirAsync(client, pf, pj.Documento, 600.00m))
        {
            var json = await ApiBank.AssertProblemaAsync(recusada, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
            AssertLimite(json, "Diario", limite: 2000.00m, disponivel: 500.00m);
        }

        using (var ate0Limite = await TransferirAsync(client, pf, pj.Documento, 500.00m))
        {
            Assert.Equal(HttpStatusCode.Created, ate0Limite.StatusCode);
        }

        using var esgotado = await TransferirAsync(client, pf, pj.Documento, 0.01m);
        var corpo = await ApiBank.AssertProblemaAsync(esgotado, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
        AssertLimite(corpo, "Diario", limite: 2000.00m, disponivel: 0.00m);
        Assert.Equal(3000.00m, await SaldoAsync(client, pf));
    }

    [Fact]
    public async Task TransferenciasRecebidas_NaoContamParaOLimite()
    {
        using var client = factory.CreateClient();
        var (pf, outraPf) = (await CriarPfAsync(client), await CriarPfAsync(client));

        using (var recebida = await TransferirAsync(client, outraPf, pf.Documento, 2000.00m))
        {
            Assert.Equal(HttpStatusCode.Created, recebida.StatusCode);
        }

        using var enviada = await TransferirAsync(client, pf, outraPf.Documento, 2000.00m);
        Assert.Equal(HttpStatusCode.Created, enviada.StatusCode);
    }

    [Fact]
    public async Task ViradaDoDiaDeBrasilia_LiberaOLimite()
    {
        using var client = factory.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        // 23h50 de 14/01/2030 em Brasília = 02h50 UTC de 15/01: já é "amanhã" em UTC.
        factory.Relogio.Agora = new DateTimeOffset(2030, 1, 15, 2, 50, 0, TimeSpan.Zero);
        using (var antes = await TransferirAsync(client, pf, pj.Documento, 2000.00m))
        {
            Assert.Equal(HttpStatusCode.Created, antes.StatusCode);
        }

        using (var mesmoDia = await TransferirAsync(client, pf, pj.Documento, 0.01m))
        {
            await ApiBank.AssertProblemaAsync(mesmoDia, HttpStatusCode.UnprocessableEntity, TipoLimiteExcedido);
        }

        // 00h10 de 15/01 em Brasília.
        factory.Relogio.Agora = new DateTimeOffset(2030, 1, 15, 3, 10, 0, TimeSpan.Zero);
        using var depois = await TransferirAsync(client, pf, pj.Documento, 2000.00m);
        Assert.Equal(HttpStatusCode.Created, depois.StatusCode);
    }

    [Fact]
    public async Task SemLimiteDiario_NaoRecusa()
    {
        await using var semDiario = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Transferencias:Limites:PessoaFisica:Diario"] = string.Empty })));
        using var client = semDiario.CreateClient();
        var (pf, pj) = (await CriarPfAsync(client), await CriarPjAsync(client));

        using var resposta = await TransferirAsync(client, pf, pj.Documento, 4000.00m);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }
}
