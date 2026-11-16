using System.Diagnostics;
using Bank.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bank.UnitTests.ErrorHandling;

public class ProblemDetailsTraceIdCustomizerTests
{
    [Fact]
    public void Customizar_ComActivityAtiva_UsaTraceIdDe32Hexadecimais()
    {
        using var activity = new Activity("teste").SetIdFormat(ActivityIdFormat.W3C).Start();
        var contexto = CriarContexto();

        ProblemDetailsTraceIdCustomizer.Customizar(contexto);

        Assert.Equal(activity.TraceId.ToHexString(), contexto.ProblemDetails.Extensions["traceId"]);
        Assert.Matches("^[0-9a-f]{32}$", (string)contexto.ProblemDetails.Extensions["traceId"]!);
    }

    [Fact]
    public void Customizar_SemActivity_UsaTraceIdentifierDaRequisicao()
    {
        Activity.Current = null;
        var contexto = CriarContexto();
        contexto.HttpContext.TraceIdentifier = "id-da-requisicao";

        ProblemDetailsTraceIdCustomizer.Customizar(contexto);

        Assert.Equal("id-da-requisicao", contexto.ProblemDetails.Extensions["traceId"]);
    }

    private static ProblemDetailsContext CriarContexto() => new()
    {
        HttpContext = new DefaultHttpContext(),
        ProblemDetails = new ProblemDetails(),
    };
}
