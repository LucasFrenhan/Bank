using System.Diagnostics;

namespace Bank.Api.ErrorHandling;

/// <summary>
/// Ajustes comuns a toda resposta <c>ProblemDetails</c>:
/// <list type="bullet">
/// <item>extensão <c>traceId</c> com o trace ID (32 hexadecimais) da requisição, pesquisável
/// no dashboard de telemetria — o padrão do ASP.NET Core grava o <c>traceparent</c> completo
/// (specs/002-setup-api, R6);</item>
/// <item><c>type</c> estável para os desafios 401/403 da autenticação, que chegam com o tipo
/// padrão do framework (specs/003-usuarios-carteiras, R4).</item>
/// </list>
/// </summary>
public static class ProblemDetailsTraceIdCustomizer
{
    public const string ChaveTraceId = "traceId";
    public const string TipoNaoAutenticado = ResultadoHttpExtensions.PrefixoTipo + "nao-autenticado";
    public const string TipoAcessoNegado = ResultadoHttpExtensions.PrefixoTipo + "acesso-negado";

    public static void Customizar(ProblemDetailsContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var problema = contexto.ProblemDetails;
        problema.Extensions[ChaveTraceId] =
            Activity.Current?.TraceId.ToHexString() ?? contexto.HttpContext.TraceIdentifier;

        if (!EhTipoPadrao(problema.Type))
        {
            return;
        }

        switch (problema.Status ?? contexto.HttpContext.Response.StatusCode)
        {
            case StatusCodes.Status401Unauthorized:
                problema.Type = TipoNaoAutenticado;
                problema.Title = "Não autenticado";
                problema.Detail ??= "Informe um token de acesso válido.";
                break;
            case StatusCodes.Status403Forbidden:
                problema.Type = TipoAcessoNegado;
                problema.Title = "Acesso negado";
                problema.Detail ??= "O token não concede acesso a este recurso.";
                break;
        }
    }

    private static bool EhTipoPadrao(string? tipo) =>
        tipo is null || tipo.StartsWith("https://tools.ietf.org/", StringComparison.Ordinal);
}
