using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bank.IntegrationTests;

/// <summary>
/// Geradores de dados válidos e únicos (CPF, CNPJ alfanumérico, e-mail): cada teste usa os
/// seus, sem depender de limpeza da base.
/// </summary>
public static class Dados
{
    public const string SenhaValida = "SenhaForte@123";

    private const string CaracteresCnpj = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Email() => $"usuario.{Guid.NewGuid():N}@exemplo.com.br";

    public static string Cpf()
    {
        var digitos = new int[11];
        do
        {
            for (var i = 0; i < 9; i++)
            {
                digitos[i] = RandomNumberGenerator.GetInt32(10);
            }
        }
        while (digitos.Take(9).Distinct().Count() == 1);

        digitos[9] = DigitoCpf(digitos, 9);
        digitos[10] = DigitoCpf(digitos, 10);
        return string.Concat(digitos);
    }

    public static string CpfComMascara(string cpf) => $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

    /// <summary>CNPJ alfanumérico (IN RFB nº 2.229/2024) em maiúsculas.</summary>
    public static string CnpjAlfanumerico()
    {
        var caracteres = new char[14];
        for (var i = 0; i < 12; i++)
        {
            caracteres[i] = CaracteresCnpj[RandomNumberGenerator.GetInt32(CaracteresCnpj.Length)];
        }

        caracteres[12] = DigitoCnpj(caracteres, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        caracteres[13] = DigitoCnpj(caracteres, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return new string(caracteres);
    }

    public static string CnpjComMascara(string cnpj) => $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";

    public static object CadastroPf(string? email = null, string? documento = null) => new
    {
        nomeCompleto = "Lucas Silva",
        email = email ?? Email(),
        documento = documento ?? Cpf(),
        tipoConta = "PessoaFisica",
        senha = SenhaValida,
    };

    public static object CadastroPj(string? email = null, string? documento = null) => new
    {
        nomeCompleto = "Empresa Exemplo Ltda",
        email = email ?? Email(),
        documento = documento ?? CnpjAlfanumerico(),
        tipoConta = "PessoaJuridica",
        senha = SenhaValida,
    };

    private static int DigitoCpf(int[] digitos, int quantidade)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += digitos[i] * (quantidade + 1 - i);
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static char DigitoCnpj(char[] caracteres, int quantidade, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (caracteres[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return (char)('0' + (resto < 2 ? 0 : 11 - resto));
    }
}

/// <summary>Chamadas comuns aos endpoints desta spec.</summary>
public static class ApiBank
{
    public static readonly Uri Registrar = new("/api/v1/autenticacao/registrar", UriKind.Relative);
    public static readonly Uri Login = new("/api/v1/autenticacao/login", UriKind.Relative);
    public static readonly Uri MinhaCarteira = new("/api/v1/carteiras/minha", UriKind.Relative);

    public static async Task<JsonElement> RegistrarAsync(HttpClient client, object cadastro)
    {
        using var resposta = await client.PostAsJsonAsync(Registrar, cadastro, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Created, resposta.StatusCode);
        return await LerJsonAsync(resposta);
    }

    public static async Task<string> ObterTokenAsync(HttpClient client, string email, string senha = Dados.SenhaValida)
    {
        using var resposta = await client.PostAsJsonAsync(Login, new { email, senha }, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        return (await LerJsonAsync(resposta)).GetProperty("tokenAcesso").GetString()!;
    }

    public static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var documento = JsonDocument.Parse(corpo);
        return documento.RootElement.Clone();
    }

    /// <summary>Confere o formato ProblemDetails (Princípio III) e devolve o corpo.</summary>
    public static async Task<JsonElement> AssertProblemaAsync(HttpResponseMessage resposta, System.Net.HttpStatusCode status, string? tipo = null)
    {
        Assert.Equal(status, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        var json = await LerJsonAsync(resposta);
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", json.GetProperty("traceId").GetString());
        if (tipo is not null)
        {
            Assert.Equal(tipo, json.GetProperty("type").GetString());
        }

        return json;
    }
}
