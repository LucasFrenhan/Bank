using Bank.Domain.Transferencias;

namespace Bank.UnitTests.Domain;

/// <summary>Regra de limites (specs/009-limite-transferencia, FR-001, FR-004; data-model).</summary>
public class LimitesTransferenciaTests
{
    private static readonly LimitesTransferencia Padrao = new(PorOperacao: 1000.00m, Diario: 2000.00m);

    [Fact]
    public void PorOperacao_ValorIgualAoLimite_Permitido()
    {
        Assert.True(Padrao.VerificarOperacao(1000.00m).Sucesso);
    }

    [Fact]
    public void PorOperacao_UmCentavoAcima_RecusaComDetalhes()
    {
        var resultado = Padrao.VerificarOperacao(1000.01m);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal("limite-excedido", erro.Codigo);
        AssertDetalhes(erro.Detalhes, "PorOperacao", limite: 1000.00m, disponivel: 1000.00m);
    }

    [Fact]
    public void Diario_SomaIgualAoLimite_Permitido()
    {
        Assert.True(Padrao.VerificarDiario(500.00m, transferidoHoje: 1500.00m).Sucesso);
    }

    [Fact]
    public void Diario_UmCentavoAcima_RecusaComDisponivel()
    {
        var resultado = Padrao.VerificarDiario(500.01m, transferidoHoje: 1500.00m);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal("limite-excedido", erro.Codigo);
        AssertDetalhes(erro.Detalhes, "Diario", limite: 2000.00m, disponivel: 500.00m);
    }

    [Fact]
    public void Diario_TransferidoAcimaDoLimite_DisponivelNuncaNegativo()
    {
        var limites = new LimitesTransferencia(PorOperacao: null, Diario: 1000.00m);

        var erro = Assert.Single(limites.VerificarDiario(0.01m, transferidoHoje: 1500.00m).Erros);

        AssertDetalhes(erro.Detalhes, "Diario", limite: 1000.00m, disponivel: 0.00m);
        Assert.Equal(0.00m, limites.DisponivelHoje(1500.00m));
    }

    [Fact]
    public void SemLimites_NuncaRecusa()
    {
        var limites = new LimitesTransferencia(PorOperacao: null, Diario: null);

        Assert.True(limites.VerificarOperacao(1_000_000m).Sucesso);
        Assert.True(limites.VerificarDiario(1_000_000m, transferidoHoje: 1_000_000m).Sucesso);
        Assert.Null(limites.DisponivelHoje(1_000_000m));
    }

    [Fact]
    public void DisponivelHoje_DiarioMenosTransferido()
    {
        Assert.Equal(500.00m, Padrao.DisponivelHoje(1500.00m));
    }

    private static void AssertDetalhes(IReadOnlyDictionary<string, object?>? detalhes, string tipo, decimal limite, decimal disponivel)
    {
        Assert.NotNull(detalhes);
        Assert.Equal(tipo, detalhes["tipoLimite"]);
        Assert.Equal(limite, detalhes["limite"]);
        Assert.Equal(disponivel, detalhes["valorDisponivel"]);
    }
}
