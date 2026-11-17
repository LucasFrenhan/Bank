using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>Contas e chamadas dos testes de transferência (specs/006-transferencias).</summary>
public sealed record ContaTeste(string Email, string Documento, Guid UsuarioId, Guid CarteiraId, string Token);

public static class ApiTransferencias
{
    public static readonly Uri Transferencias = new("/api/v1/transferencias", UriKind.Relative);
    public static readonly Uri ConsultarDestinatario = new("/api/v1/transferencias/destinatarios/consultar", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ContaTeste> CriarPfAsync(HttpClient client, string? documento = null)
    {
        var email = Dados.Email();
        documento ??= Dados.Cpf();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email, documento));
        return await ContaAsync(client, email, documento, cadastro);
    }

    public static async Task<ContaTeste> CriarPjAsync(HttpClient client)
    {
        var email = Dados.Email();
        var documento = Dados.CnpjAlfanumerico();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPj(email, documento));
        return await ContaAsync(client, email, documento, cadastro);
    }

    public static async Task<HttpResponseMessage> TransferirAsync(HttpClient client, ContaTeste pagador, object corpo, Guid? chave = null, bool enviarChave = true)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Transferencias) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pagador.Token);
        if (enviarChave)
        {
            requisicao.Headers.Add("Idempotency-Key", (chave ?? Guid.NewGuid()).ToString());
        }

        return await client.SendAsync(requisicao, Ct);
    }

    public static Task<HttpResponseMessage> TransferirAsync(HttpClient client, ContaTeste pagador, string documentoDestino, decimal valor, Guid? chave = null, string? descricao = null) =>
        TransferirAsync(client, pagador, new { documentoDestino, valor, descricao }, chave);

    public static async Task<HttpResponseMessage> GetComTokenAsync(HttpClient client, string token, string caminho)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri(caminho, UriKind.Relative));
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(requisicao, Ct);
    }

    public static async Task<HttpResponseMessage> PostComTokenAsync(HttpClient client, string token, Uri caminho, object corpo)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, caminho) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(requisicao, Ct);
    }

    public static async Task<decimal> SaldoAsync(HttpClient client, ContaTeste conta)
    {
        using var resposta = await GetComTokenAsync(client, conta.Token, "/api/v1/carteiras/minha");
        return (await ApiBank.LerJsonAsync(resposta)).GetProperty("saldo").GetDecimal();
    }

    /// <summary>Saldo − (entradas − saídas) da carteira: deve ser zero (FR-015).</summary>
    public static async Task<decimal> DiferencaSaldoRazaoAsync(BankApiFactory factory, Guid carteiraId)
    {
        await using var contexto = factory.CriarContexto();
        var saldo = await contexto.Carteiras.Where(c => c.Id == carteiraId).Select(c => c.Saldo).SingleAsync(Ct);
        var razao = await contexto.Lancamentos.Where(l => l.CarteiraId == carteiraId)
            .SumAsync(l => l.Direcao == Bank.Domain.Transferencias.DirecaoLancamento.Entrada ? l.Valor : -l.Valor, Ct);
        return saldo - razao;
    }

    private static async Task<ContaTeste> ContaAsync(HttpClient client, string email, string documento, JsonElement cadastro) =>
        new(email,
            cadastro.GetProperty("documento").GetString() ?? documento,
            cadastro.GetProperty("usuarioId").GetGuid(),
            cadastro.GetProperty("carteiraId").GetGuid(),
            await ApiBank.ObterTokenAsync(client, email));
}
