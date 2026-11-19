using Bank.Application.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Consulta de limites (specs/009-limite-transferencia, US4; FR-012; research R6, R7).</summary>
public sealed class ConsultarLimitesTests : IDisposable
{
    /// <summary>10h de 18/11 em Brasília.</summary>
    private static readonly RelogioFixo Relogio = new(new DateTimeOffset(2026, 11, 18, 13, 0, 0, TimeSpan.Zero));

    private readonly BancoFalso _banco = new();
    private readonly MetricasCapturadasTransferencias _metricas = new();
    private readonly LimitesTransferenciaOptions _limites = new() { PessoaFisica = new() { PorOperacao = 1000m, Diario = 5000m } };
    private readonly ConsultarLimites _casoDeUso;

    public ConsultarLimitesTests()
    {
        _casoDeUso = new ConsultarLimites(_banco, _banco, new MonitorDeOpcoes<LimitesTransferenciaOptions>(_limites), new CalendarioBrasilia(), Relogio, _metricas.Metricas);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task ComLimites_DevolveResumoDoDiaDeBrasilia()
    {
        var pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
        _banco.TransferidoAntes = 4500m;

        var resultado = await _casoDeUso.ExecutarAsync(pf.Id, CancellationToken.None);

        Assert.Equal(new ResumoLimites(1000m, 5000m, 4500m, 500m, new DateOnly(2026, 11, 18)), resultado.Valor);
        Assert.Equal(
            (new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 19, 3, 0, 0, TimeSpan.Zero)),
            _banco.UltimoIntervalo);
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.limites.consultas" && m.Tags["resultado"] == "consultada");
    }

    [Fact]
    public async Task TransferidoAcimaDoDiario_DisponivelZero()
    {
        var pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
        _banco.TransferidoAntes = 6000m;

        var resultado = await _casoDeUso.ExecutarAsync(pf.Id, CancellationToken.None);

        Assert.Equal(0m, resultado.Valor.DisponivelHoje);
    }

    [Fact]
    public async Task SemLimites_NulosEDisponivelNulo()
    {
        _limites.PessoaFisica = new LimitesPerfilOptions();
        var pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);

        var resultado = await _casoDeUso.ExecutarAsync(pf.Id, CancellationToken.None);

        Assert.Equal((null, null, null), (resultado.Valor.PorOperacao, resultado.Valor.Diario, resultado.Valor.DisponivelHoje));
        Assert.Equal(0m, resultado.Valor.TransferidoHoje);
    }

    [Fact]
    public async Task UsuarioSemCarteira_UsuarioNaoEncontrado()
    {
        var resultado = await _casoDeUso.ExecutarAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrosUsuario.NaoEncontrado, Assert.Single(resultado.Erros));
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.limites.consultas" && m.Tags["resultado"] == "usuario-nao-encontrado");
    }
}
