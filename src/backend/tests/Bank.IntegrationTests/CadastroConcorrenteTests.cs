using System.Net;
using System.Net.Http.Json;

namespace Bank.IntegrationTests;

/// <summary>SC-004: unicidade garantida pela base sob cadastros concorrentes (FR-008).</summary>
public class CadastroConcorrenteTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private const int Tentativas = 50;

    [Fact]
    public async Task MesmoEmail_50Concorrentes_Exatamente1Criado()
    {
        var email = Dados.Email();

        var status = await EnviarConcorrentesAsync(_ => Dados.CadastroPf(email, Dados.Cpf()));

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(Tentativas - 1, status.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task MesmoDocumento_50Concorrentes_Exatamente1Criado()
    {
        var cpf = Dados.Cpf();

        var status = await EnviarConcorrentesAsync(_ => Dados.CadastroPf(Dados.Email(), cpf));

        Assert.Equal(1, status.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(Tentativas - 1, status.Count(s => s == HttpStatusCode.Conflict));
    }

    private async Task<HttpStatusCode[]> EnviarConcorrentesAsync(Func<int, object> cadastro)
    {
        using var client = factory.CreateClient();
        using var largada = new SemaphoreSlim(0);

        var tarefas = Enumerable.Range(0, Tentativas).Select(async i =>
        {
            await largada.WaitAsync(TestContext.Current.CancellationToken);
            using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, cadastro(i), TestContext.Current.CancellationToken);
            return resposta.StatusCode;
        }).ToArray();

        largada.Release(Tentativas);
        return await Task.WhenAll(tarefas);
    }
}
