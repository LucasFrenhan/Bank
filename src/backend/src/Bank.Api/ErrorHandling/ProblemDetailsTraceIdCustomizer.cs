using System.Diagnostics;

namespace Bank.Api.ErrorHandling;

/// <summary>
/// Define a extensão <c>traceId</c> das respostas <c>ProblemDetails</c> com o trace ID
/// (32 hexadecimais) da requisição, pesquisável diretamente no dashboard de telemetria.
/// O padrão do ASP.NET Core grava o <c>traceparent</c> completo (specs/002-setup-api, R6).
/// </summary>
public static class ProblemDetailsTraceIdCustomizer
{
    public const string ChaveTraceId = "traceId";

    public static void Customizar(ProblemDetailsContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        contexto.ProblemDetails.Extensions[ChaveTraceId] =
            Activity.Current?.TraceId.ToHexString() ?? contexto.HttpContext.TraceIdentifier;
    }
}
