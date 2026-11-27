namespace Bank.Api.Seguranca;

/// <summary>
/// Cookie da credencial de renovação (specs/016-sessao-refresh-token, FR-003, FR-022; research R4;
/// contracts/autenticacao-sessao-api.md). Transporte HTTP: o valor nunca vai para o corpo, log ou erro.
/// </summary>
public static class CookieRenovacao
{
    public const string Nome = "bank_renovacao";
    public const string Caminho = "/api/v1/autenticacao";

    public static string? Ler(HttpRequest requisicao)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        return requisicao.Cookies.TryGetValue(Nome, out var valor) && !string.IsNullOrEmpty(valor) ? valor : null;
    }

    public static void Escrever(HttpResponse resposta, string valor, int maxAgeSegundos)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        resposta.Cookies.Append(Nome, valor, Opcoes(TimeSpan.FromSeconds(maxAgeSegundos)));
    }

    public static void Remover(HttpResponse resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        resposta.Cookies.Append(Nome, string.Empty, Opcoes(TimeSpan.Zero));
    }

    private static CookieOptions Opcoes(TimeSpan maxAge) => new()
    {
        Path = Caminho,
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        MaxAge = maxAge,
        IsEssential = true,
    };
}
