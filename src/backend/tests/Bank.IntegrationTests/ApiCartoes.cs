using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>PF com cartão e os dados exibidos ao titular.</summary>
public sealed record CartaoTeste(ContaTeste Conta, string Numero, string Validade, string Cvv)
{
    public string UltimosDigitos => Numero[^4..];

    public object Corpo(string? numero = null, string? validade = null, string? cvv = null, string? titular = "LUCAS SILVA") =>
        new { nomeTitularCartao = titular, numeroCartao = numero ?? Numero, validade = validade ?? Validade, cvv = cvv ?? Cvv };
}

/// <summary>Chamadas aos endpoints de cartão, cobrança, fatura e recebível (specs/014-cartao-credito-cobrancas).</summary>
public static class ApiCartoes
{
    public const string Cartoes = "/api/v1/cartoes";
    public const string MeuCartao = "/api/v1/cartoes/meu";
    public const string MeusDados = "/api/v1/cartoes/meu/dados";
    public const string Cobrancas = "/api/v1/cobrancas";
    public const string MinhasCobrancas = "/api/v1/cobrancas/minhas";
    public const string Faturas = "/api/v1/faturas";
    public const string Recebiveis = "/api/v1/recebiveis";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Instante no horário de Brasília (UTC−3).</summary>
    public static DateTimeOffset Brasilia(int ano, int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) =>
        new DateTimeOffset(ano, mes, dia, hora, minuto, segundo, TimeSpan.FromHours(-3)).ToUniversalTime();

    /// <summary><c>urn:bank:problema:{codigo}</c>.</summary>
    public static string Tipo(string codigo) => "urn:bank:problema:" + codigo;

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string caminho, object? corpo = null, Guid? chave = null)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(caminho, UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo);
        }

        if (chave is not null)
        {
            requisicao.Headers.Add("Idempotency-Key", chave.Value.ToString());
        }

        return await client.SendAsync(requisicao, Ct);
    }

    public static async Task<JsonElement> GetOkAsync(HttpClient client, string token, string caminho)
    {
        using var resposta = await GetComTokenAsync(client, token, caminho);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await ApiBank.LerJsonAsync(resposta);
    }

    public static async Task<JsonElement> PostOkAsync(HttpClient client, string token, string caminho, object? corpo = null, Guid? chave = null, HttpStatusCode status = HttpStatusCode.OK)
    {
        using var resposta = await PostAsync(client, token, caminho, corpo, chave);
        Assert.Equal(status, resposta.StatusCode);
        return await ApiBank.LerJsonAsync(resposta);
    }

    public static Task<HttpResponseMessage> SolicitarAsync(HttpClient client, ContaTeste conta, string? bandeira = "001") =>
        PostAsync(client, conta.Token, Cartoes, new { bandeira });

    /// <summary>PF nova, no horário real (o token é validado pelo relógio do sistema), com cartão.</summary>
    public static async Task<CartaoTeste> CriarPfComCartaoAsync(CartoesApiFactory factory, HttpClient client, string bandeira = "001")
    {
        var conta = await NoHorarioRealAsync(factory, () => CriarPfAsync(client));
        return await EmitirCartaoAsync(client, conta, bandeira);
    }

    public static Task<ContaTeste> CriarLojistaAsync(CartoesApiFactory factory, HttpClient client) =>
        NoHorarioRealAsync(factory, () => CriarPjAsync(client));

    public static async Task<CartaoTeste> EmitirCartaoAsync(HttpClient client, ContaTeste conta, string bandeira = "001")
    {
        await PostOkAsync(client, conta.Token, Cartoes, new { bandeira }, status: HttpStatusCode.Created);
        var dados = await GetOkAsync(client, conta.Token, MeusDados);
        return new CartaoTeste(conta, dados.GetProperty("numero").GetString()!, dados.GetProperty("validade").GetString()!, dados.GetProperty("cvv").GetString()!);
    }

    public static async Task<Guid> EmitirAsync(HttpClient client, ContaTeste lojista, decimal valor = 299.90m, string descricao = "Café Especial") =>
        (await PostOkAsync(client, lojista.Token, Cobrancas, new { valor, descricao }, status: HttpStatusCode.Created)).GetProperty("cobrancaId").GetGuid();

    public static Task<HttpResponseMessage> PagarAsync(HttpClient client, CartaoTeste cartao, Guid cobrancaId, Guid? chave = null, object? corpo = null) =>
        PostAsync(client, cartao.Conta.Token, $"{Cobrancas}/{cobrancaId}/pagar", corpo ?? cartao.Corpo(), chave ?? Guid.NewGuid());

    /// <summary>Emite e paga uma cobrança; devolve o identificador dela.</summary>
    public static async Task<Guid> ComprarAsync(HttpClient client, CartaoTeste cartao, ContaTeste lojista, decimal valor = 299.90m, string descricao = "Café Especial")
    {
        var cobranca = await EmitirAsync(client, lojista, valor, descricao);
        using var resposta = await PagarAsync(client, cartao, cobranca);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return cobranca;
    }

    public static async Task<decimal> LimiteDisponivelAsync(HttpClient client, CartaoTeste cartao) =>
        (await GetOkAsync(client, cartao.Conta.Token, MeuCartao)).GetProperty("limiteDisponivel").GetDecimal();

    public static Task<JsonElement> FaturaAsync(HttpClient client, CartaoTeste cartao, string competencia) =>
        GetOkAsync(client, cartao.Conta.Token, $"{Faturas}/{competencia}");

    public static Task<HttpResponseMessage> PagarFaturaAsync(HttpClient client, CartaoTeste cartao, string competencia, Guid? chave = null) =>
        PostAsync(client, cartao.Conta.Token, $"{Faturas}/{competencia}/pagar", chave: chave ?? Guid.NewGuid());

    public static async Task<JsonElement> RecebivelDaCobrancaAsync(HttpClient client, ContaTeste lojista, Guid cobrancaId) =>
        (await GetOkAsync(client, lojista.Token, Recebiveis + "?tamanhoPagina=100")).GetProperty("itens").EnumerateArray()
            .Single(r => r.GetProperty("cobrancaId").GetGuid() == cobrancaId);

    public static Task<HttpResponseMessage> AnteciparAsync(HttpClient client, ContaTeste lojista, Guid recebivelId, Guid? chave = null) =>
        PostAsync(client, lojista.Token, $"{Recebiveis}/{recebivelId}/antecipar", chave: chave ?? Guid.NewGuid());

    private static async Task<T> NoHorarioRealAsync<T>(CartoesApiFactory factory, Func<Task<T>> acao)
    {
        var ajustado = factory.Relogio.Agora;
        factory.Relogio.Agora = null;
        try
        {
            return await acao();
        }
        finally
        {
            factory.Relogio.Agora = ajustado;
        }
    }
}
