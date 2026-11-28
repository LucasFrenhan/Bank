using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.UnitTests.Application;

/// <summary>Consulta prévia do destinatário (spec 006, US1a; FR-009a/FR-009b).</summary>
public sealed class ConsultarDestinatarioTests : IDisposable
{
    private readonly BancoFalso _banco = new();
    private readonly MetricasCapturadasTransferencias _metricas = new();
    private readonly ConsultarDestinatario _casoDeUso;
    private readonly Usuario _pf;

    public ConsultarDestinatarioTests()
    {
        _casoDeUso = new ConsultarDestinatario(_banco, _metricas.Metricas);
        _pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
        _banco.Criar("Cafeteria Grão Especial Ltda", "12ABC34501DE35", TipoConta.PessoaJuridica, 0m);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task Encontrado_DevolveNomeCompletoETipo()
    {
        var resultado = await _casoDeUso.ExecutarAsync(_pf.Id, "12.abc.345/01de-35", CancellationToken.None);

        Assert.Equal(new DestinatarioConsultado("Cafeteria Grão Especial Ltda", TipoConta.PessoaJuridica), resultado.Valor);
        AssertMetrica("encontrado");
    }

    [Fact]
    public async Task Inexistente_404()
    {
        var resultado = await _casoDeUso.ExecutarAsync(_pf.Id, "11222333000181", CancellationToken.None);

        Assert.Equal(ErrosTransferencia.DestinatarioNaoEncontrado, Assert.Single(resultado.Erros));
        AssertMetrica("nao-encontrado");
    }

    [Fact]
    public async Task ProprioDocumento_Autotransferencia()
    {
        var resultado = await _casoDeUso.ExecutarAsync(_pf.Id, "529.982.247-25", CancellationToken.None);

        Assert.Equal(ErrosTransferencia.Autotransferencia, Assert.Single(resultado.Erros));
        AssertMetrica("autotransferencia");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("12ABC34501DE36")]
    public async Task Invalido_ErroNoCampoDocumento(string? documento)
    {
        var resultado = await _casoDeUso.ExecutarAsync(_pf.Id, documento, CancellationToken.None);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal((TipoErro.Validacao, "documento"), (erro.Tipo, erro.Campo));
        AssertMetrica("invalido");
    }

    private void AssertMetrica(string resultado) =>
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.destinatario.consultas" && m.Tags["resultado"] == resultado);
}
