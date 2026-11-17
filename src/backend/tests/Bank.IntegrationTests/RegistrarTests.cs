using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>POST /api/v1/autenticacao/registrar (US1; contracts/usuarios-carteiras-api.md).</summary>
public class RegistrarTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PessoaFisica_ComCpfMascarado_201ComCorpoDoContrato()
    {
        using var client = factory.CreateClient();
        var cpf = Dados.Cpf();
        var email = Dados.Email();

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, Dados.CadastroPf(email.ToUpperInvariant(), Dados.CpfComMascara(cpf)), Ct);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal("/api/v1/carteiras/minha", resposta.Headers.Location?.OriginalString);
        var corpo = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\"saldoInicial\":10.00", corpo, StringComparison.Ordinal);

        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(7, json.GetProperty("usuarioId").GetGuid().Version);
        Assert.Equal(7, json.GetProperty("carteiraId").GetGuid().Version);
        Assert.Equal("Lucas Silva", json.GetProperty("nomeCompleto").GetString());
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.Equal(cpf, json.GetProperty("documento").GetString());
        Assert.Equal("PessoaFisica", json.GetProperty("tipoConta").GetString());
        Assert.Equal("BRL", json.GetProperty("moeda").GetString());
        Assert.False(json.TryGetProperty("senha", out _));
    }

    [Fact]
    public async Task PessoaJuridica_ComCnpjAlfanumericoMinusculoMascarado_201ComSaldoZero()
    {
        using var client = factory.CreateClient();
        var cnpj = Dados.CnpjAlfanumerico();

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, Dados.CadastroPj(documento: Dados.CnpjComMascara(cnpj).ToLowerInvariant()), Ct);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Contains("\"saldoInicial\":0.00", await resposta.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(cnpj, json.GetProperty("documento").GetString());
        Assert.Equal("PessoaJuridica", json.GetProperty("tipoConta").GetString());
    }

    [Fact]
    public async Task CampoSaldoExtra_EIgnorado()
    {
        using var client = factory.CreateClient();
        var cadastro = new { nomeCompleto = "Lucas", email = Dados.Email(), documento = Dados.Cpf(), tipoConta = "PessoaFisica", senha = Dados.SenhaValida, saldo = 1000000 };

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, cadastro, Ct);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Contains("\"saldoInicial\":10.00", await resposta.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorpoSemCampos_400ComTodosOsCampos()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, new { }, Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        var campos = json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["documento", "email", "nomeCompleto", "senha", "tipoConta"], campos);
    }

    [Theory]
    [InlineData("PF", "52998224725", "tipoConta")]
    [InlineData("PessoaJuridica", "52998224725", "documento")]           // CPF com tipo PJ
    [InlineData("PessoaFisica", "12ABC34501DE35", "documento")]          // CNPJ com tipo PF
    [InlineData("PessoaJuridica", "12ABC34501DE36", "documento")]        // DV inválido
    [InlineData("PessoaFisica", "11111111111", "documento")]             // dígitos repetidos
    public async Task CampoInvalido_400ComErroNoCampo(string tipoConta, string documento, string campo)
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        var cadastro = new { nomeCompleto = "Lucas", email, documento, tipoConta, senha = Dados.SenhaValida };

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, cadastro, Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty(campo, out _));
        await AssertNenhumUsuarioAsync(email);
    }

    [Theory]
    [InlineData("{ \"nomeCompleto\": ")]
    [InlineData("")]
    public async Task CorpoMalformadoOuVazio_400ProblemDetails(string corpo)
    {
        using var client = factory.CreateClient();
        using var conteudo = new StringContent(corpo, Encoding.UTF8, "application/json");

        using var resposta = await client.PostAsync(ApiBank.Registrar, conteudo, Ct);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmailJaCadastradoComOutraCaixa_409()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        var documentoNovo = Dados.Cpf();

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, Dados.CadastroPf(email.ToUpperInvariant(), documentoNovo), Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, "urn:bank:problema:email-ja-cadastrado");
        Assert.DoesNotContain(email, json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.Usuarios.AnyAsync(u => u.Documento == documentoNovo, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DocumentoJaCadastradoComOuSemMascara_409(bool comMascara)
    {
        using var client = factory.CreateClient();
        var cnpj = Dados.CnpjAlfanumerico();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPj(documento: cnpj));
        var emailNovo = Dados.Email();

        using var resposta = await client.PostAsJsonAsync(
            ApiBank.Registrar,
            Dados.CadastroPj(emailNovo, comMascara ? Dados.CnpjComMascara(cnpj).ToLowerInvariant() : cnpj),
            Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, "urn:bank:problema:documento-ja-cadastrado");
        Assert.DoesNotContain(cnpj, json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await AssertNenhumUsuarioAsync(emailNovo);
    }

    private async Task AssertNenhumUsuarioAsync(string email)
    {
        var normalizado = email.ToLowerInvariant();
        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.Usuarios.AnyAsync(u => u.Email == normalizado, Ct));
    }
}
