using System.Net;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>POST /api/v1/transferencias/destinatarios/consultar — US1a da spec 006.</summary>
public class DestinatarioTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CnpjAlfanumericoMinusculoEMascarado_200SoComNomeETipo()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);

        using var resposta = await PostComTokenAsync(client, pf.Token, ConsultarDestinatario, new { documento = Dados.CnpjComMascara(pj.Documento).ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(["nome", "tipoConta"], json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Empresa Exemplo Ltda", "PessoaJuridica"), (json.GetProperty("nome").GetString(), json.GetProperty("tipoConta").GetString()));
    }

    [Fact]
    public async Task Cpf_200ComNomeCompleto()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var outra = await CriarPfAsync(client);

        using var resposta = await PostComTokenAsync(client, pf.Token, ConsultarDestinatario, new { documento = outra.Documento });

        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(("Lucas Silva", "PessoaFisica"), (json.GetProperty("nome").GetString(), json.GetProperty("tipoConta").GetString()));
    }

    [Fact]
    public async Task Erros_404_422_400()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var inexistente = await PostComTokenAsync(client, pf.Token, ConsultarDestinatario, new { documento = Dados.Cpf() });
        using var proprio = await PostComTokenAsync(client, pf.Token, ConsultarDestinatario, new { documento = pf.Documento });
        using var invalido = await PostComTokenAsync(client, pf.Token, ConsultarDestinatario, new { documento = "12ABC34501DE36" });

        await ApiBank.AssertProblemaAsync(inexistente, HttpStatusCode.NotFound, "urn:bank:problema:destinatario-nao-encontrado");
        await ApiBank.AssertProblemaAsync(proprio, HttpStatusCode.UnprocessableEntity, "urn:bank:problema:autotransferencia");
        var json = await ApiBank.AssertProblemaAsync(invalido, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty("documento", out _));
    }

    [Fact]
    public async Task PessoaJuridica_403_SemToken_401()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var pf = await CriarPfAsync(client);

        using var dePj = await PostComTokenAsync(client, pj.Token, ConsultarDestinatario, new { documento = pf.Documento });
        using var semToken = await client.PostAsync(ConsultarDestinatario, null, Ct);

        await ApiBank.AssertProblemaAsync(dePj, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
        await ApiBank.AssertProblemaAsync(semToken, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }
}
