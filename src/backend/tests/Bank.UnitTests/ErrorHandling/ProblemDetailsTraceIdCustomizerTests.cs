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

    [Theory]
    [InlineData(401, "urn:bank:problema:nao-autenticado")]
    [InlineData(403, "urn:bank:problema:acesso-negado")]
    public void Customizar_DesafioComTipoPadrao_UsaTipoEstavel(int status, string tipo)
    {
        var contexto = CriarContexto(new ProblemDetails { Status = status, Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2" });

        ProblemDetailsTraceIdCustomizer.Customizar(contexto);

        Assert.Equal(tipo, contexto.ProblemDetails.Type);
    }

    [Fact]
    public void Customizar_401ComTipoDeNegocio_MantemOTipo()
    {
        var contexto = CriarContexto(new ProblemDetails { Status = 401, Type = "urn:bank:problema:credenciais-invalidas" });

        ProblemDetailsTraceIdCustomizer.Customizar(contexto);

        Assert.Equal("urn:bank:problema:credenciais-invalidas", contexto.ProblemDetails.Type);
    }

    [Fact]
    public void Customizar_404PadraoDoFramework_NaoAlteraOTipo()
    {
        var tipoPadrao = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
        var contexto = CriarContexto(new ProblemDetails { Status = 404, Type = tipoPadrao });

        ProblemDetailsTraceIdCustomizer.Customizar(contexto);

        Assert.Equal(tipoPadrao, contexto.ProblemDetails.Type);
    }

    private static ProblemDetailsContext CriarContexto(ProblemDetails? problema = null) => new()
    {
        HttpContext = new DefaultHttpContext(),
        ProblemDetails = problema ?? new ProblemDetails(),
    };
}
