using Bank.Api.ErrorHandling;
using Bank.Domain.Transferencias;
using Bank.SharedKernel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bank.UnitTests.ErrorHandling;

/// <summary>Detalhes do erro viram extensões do ProblemDetails (specs/009-limite-transferencia, R5).</summary>
public class ResultadoHttpExtensionsTests
{
    [Fact]
    public void LimiteExcedido_ViraProblemaComExtensoes()
    {
        var resultado = Resultado.Falha(ErrosTransferencia.LimiteExcedido(TipoLimite.Diario, 2000.00m, 500.00m));

        var problema = Assert.IsType<ProblemHttpResult>(resultado.ParaProblema()).ProblemDetails;

        Assert.Equal(422, problema.Status);
        Assert.Equal("urn:bank:problema:limite-excedido", problema.Type);
        Assert.Equal("Limite de transferência excedido", problema.Title);
        Assert.Equal("O valor passa do limite diário de transferências.", problema.Detail);
        Assert.Equal("Diario", problema.Extensions["tipoLimite"]);
        Assert.Equal(2000.00m, problema.Extensions["limite"]);
        Assert.Equal(500.00m, problema.Extensions["valorDisponivel"]);
    }

    [Fact]
    public void ErroSemDetalhes_SemExtensoesExtras()
    {
        var resultado = Resultado.Falha(ErrosTransferencia.SaldoInsuficiente);

        var problema = Assert.IsType<ProblemHttpResult>(resultado.ParaProblema()).ProblemDetails;

        Assert.Equal("urn:bank:problema:saldo-insuficiente", problema.Type);
        Assert.Empty(problema.Extensions);
    }
}
