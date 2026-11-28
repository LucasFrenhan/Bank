namespace Bank.ArchitectureTests.Modulos;

/// <summary>
/// Prova de que cada regra de fronteira detecta a violação que descreve (specs/017-modulos-fundacao, FR-014,
/// SC-003): avalia os módulos de exemplo (tests/Bank.ArchitectureTests.Exemplos) e exige exatamente as
/// violações plantadas em Violacoes/, nenhuma em Alfa e Beta e uma única regra por dependência.
/// </summary>
public class DeteccaoRegrasTests
{
    private const string Alfa = "Bank.Modulos.Alfa";
    private const string Gama = "Bank.Modulos.Gama";
    private const string Conversor = "Bank.SharedKernel.Infrastructure.Exemplo.ConversorDeExemplo";

    private static readonly Violacao[] Esperadas =
    [
        new("MOD-01", $"{Gama}.Application.UsaDominioDeAlfa", $"{Alfa}.Domain.Alfa"),
        new("MOD-02", $"{Gama}.Infrastructure.UsaAlfaDbContext", $"{Alfa}.Infrastructure.AlfaDbContext"),
        new("MOD-03", $"{Gama}.Application.UsaNucleoAntigo", "Bank.Domain.Exemplo.TipoDoNucleoAntigo"),
        new("MOD-04", $"{Gama}.Domain.UsaInfraDoProprioModulo", $"{Gama}.Infrastructure.RecursoDaInfra"),
        new("MOD-04", $"{Gama}.Contratos.ContratoUsaInterno", $"{Gama}.Infrastructure.RecursoDaInfra"),
        new("MOD-05", "Bank.SharedKernel.Exemplo.KernelUsaModulo", $"{Alfa}.Contratos.AlfaResumo"),
        new("MOD-05", "Bank.SharedKernel.Infrastructure.Exemplo.InfraComumUsaModulo", $"{Alfa}.Contratos.AlfaResumo"),
        new("MOD-06", "Bank.Api.Exemplo.HostUsaInterno", $"{Alfa}.Domain.Alfa"),
        new("MOD-07", $"{Gama}.Domain.UsaInfraComum", Conversor),
        new("MOD-07", $"{Gama}.Endpoints.EndpointUsaInfraComum", Conversor),
    ];

    private static readonly IReadOnlyList<Violacao> Encontradas = RegrasFronteira.Avaliar(CarregadorArquitetura.Exemplos());

    [Fact]
    public void Exemplos_GeramExatamenteAsViolacoesPlantadas()
    {
        var faltando = Esperadas.Except(Encontradas).ToList();
        var inesperadas = Encontradas.Except(Esperadas).ToList();

        Assert.True(faltando.Count == 0 && inesperadas.Count == 0,
            $"Faltando:{Environment.NewLine}{RegrasFronteira.Descrever(faltando)}{Environment.NewLine}" +
            $"Inesperadas:{Environment.NewLine}{RegrasFronteira.Descrever(inesperadas)}");
    }

    [Theory]
    [InlineData("MOD-01")]
    [InlineData("MOD-02")]
    [InlineData("MOD-03")]
    [InlineData("MOD-04")]
    [InlineData("MOD-05")]
    [InlineData("MOD-06")]
    [InlineData("MOD-07")]
    public void CadaRegra_DetectaAoMenosUmaViolacao(string regra) =>
        Assert.Contains(Encontradas, v => v.Regra == regra);

    [Fact]
    public void ModulosConformes_NaoGeramViolacoes() =>
        Assert.DoesNotContain(Encontradas, v =>
            v.Origem.StartsWith("Bank.Modulos.Alfa.", StringComparison.Ordinal) ||
            v.Origem.StartsWith("Bank.Modulos.Beta.", StringComparison.Ordinal));

    [Fact]
    public void UmaDependencia_RecebeSoARegraMaisEspecifica() =>
        Assert.Single(Encontradas, v => v.Origem == $"{Gama}.Infrastructure.UsaAlfaDbContext");

    [Fact]
    public void Descricao_IdentificaRegraOrigemEDestino()
    {
        var texto = RegrasFronteira.Descrever([Esperadas[0]]);

        Assert.Contains("Violações de fronteira (constituição, Princípio VIII):", texto, StringComparison.Ordinal);
        Assert.Contains(
            $"MOD-01 Módulo usa algo de outro módulo fora do contrato público: {Gama}.Application.UsaDominioDeAlfa → {Alfa}.Domain.Alfa",
            texto, StringComparison.Ordinal);
    }
}
