using Bank.Application.Autenticacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.IntegrationTests;

/// <summary>
/// Limpeza das sessões encerradas (specs/016-sessao-refresh-token, FR-013): revogadas, vencidas pelo limite absoluto ou
/// por inatividade há mais de 7 dias somem com as credenciais; as demais ficam.
/// </summary>
public class LimpezaSessoesTests(SessaoApiFactory factory) : IClassFixture<SessaoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RemoveSoAsEncerradasHaMaisQueARetencao()
    {
        var inicio = new DateTimeOffset(2031, 5, 1, 12, 0, 0, TimeSpan.Zero);
        using var client = factory.CreateClient();

        // Revogada (logout) no início; inativa desde o início; ativa (renovada no fim).
        factory.Relogio.Agora = inicio;
        var (email, revogada) = await ApiSessao.CadastrarEEntrarAsync(client);
        (await ApiSessao.SairAsync(client, revogada.Cookie.Valor)).Dispose();
        var inativa = await ApiSessao.EntrarAsync(client, email);
        var ativa = await ApiSessao.EntrarAsync(client, email);

        // Revogada há 6 dias: ainda fica.
        factory.Relogio.Agora = inicio.AddDays(6);
        Assert.Equal(0, await LimparAsync());

        // A "ativa" segue em uso logo antes da limpeza, num login novo (a anterior expirou pelo limite absoluto).
        factory.Relogio.Agora = inicio.AddDays(8);
        var recente = await ApiSessao.EntrarAsync(client, email);

        var removidas = await LimparAsync();

        Assert.Equal(3, removidas); // revogada, inativa e a "ativa" vencida há mais de 7 dias
        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.CredenciaisRenovacao.AnyAsync(c => c.Hash == RenovacaoTests.Hash(revogada.Cookie.Valor), Ct));
        Assert.False(await contexto.CredenciaisRenovacao.AnyAsync(c => c.Hash == RenovacaoTests.Hash(inativa.Cookie.Valor), Ct));
        Assert.False(await contexto.CredenciaisRenovacao.AnyAsync(c => c.Hash == RenovacaoTests.Hash(ativa.Cookie.Valor), Ct));
        Assert.True(await contexto.CredenciaisRenovacao.AnyAsync(c => c.Hash == RenovacaoTests.Hash(recente.Cookie.Valor), Ct));
        factory.Relogio.Agora = null;
    }

    private async Task<int> LimparAsync()
    {
        await using var escopo = factory.CriarEscopo();
        return await escopo.ServiceProvider.GetRequiredService<LimparSessoes>().ExecutarAsync(Ct);
    }
}
