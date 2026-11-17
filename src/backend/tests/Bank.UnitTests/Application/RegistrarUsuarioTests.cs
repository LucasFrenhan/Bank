using Bank.Application.Abstracoes;
using Bank.Application.Usuarios;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.Application;

public sealed class RegistrarUsuarioTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly RepositorioFalso _repositorio = new();
    private readonly HasherFalso _hasher = new();
    private readonly MetricasCapturadas _metricas = new();
    private readonly RegistrarUsuario _casoDeUso;

    public RegistrarUsuarioTests()
    {
        _casoDeUso = new RegistrarUsuario(
            _repositorio,
            _hasher,
            Options.Create(new SaldoInicialOptions { PessoaFisica = 10m, PessoaJuridica = 0m }),
            new RelogioFixo(Agora),
            _metricas.Metricas,
            NullLogger<RegistrarUsuario>.Instance);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task Executar_PessoaFisicaValida_CriaComSaldoDasOpcoes()
    {
        var resultado = await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        var registrado = resultado.Valor;
        Assert.Equal("lucas@exemplo.com.br", registrado.Email);
        Assert.Equal("52998224725", registrado.Documento);
        Assert.Equal(TipoConta.PessoaFisica, registrado.TipoConta);
        Assert.Equal("10.00", registrado.SaldoInicial.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("BRL", registrado.Moeda);
        Assert.Single(_repositorio.Usuarios);
    }

    [Fact]
    public async Task Executar_PessoaJuridicaValida_CriaComSaldoZero()
    {
        var comando = new RegistrarUsuarioComando("Empresa Exemplo Ltda", "contato@exemplo.com.br", "12.abc.345/01de-35", "PessoaJuridica", "SenhaForte@123");

        var registrado = (await _casoDeUso.ExecutarAsync(comando, CancellationToken.None)).Valor;

        Assert.Equal("12ABC34501DE35", registrado.Documento);
        Assert.Equal(0.00m, registrado.SaldoInicial);
    }

    [Fact]
    public async Task Executar_TodosOsCamposInvalidos_DevolveTodosOsErrosDeUmaVez()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new RegistrarUsuarioComando(" ", "invalido", "123", "PF", "fraca"), CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.All(resultado.Erros, e => Assert.Equal(TipoErro.Validacao, e.Tipo));
        Assert.Equal(
            ["documento", "email", "nomeCompleto", "senha", "tipoConta"],
            resultado.Erros.Select(e => e.Campo!).Distinct().Order(StringComparer.Ordinal));
        Assert.Empty(_repositorio.Usuarios);
    }

    [Fact]
    public async Task Executar_EmailJaExistente_EmailJaCadastrado()
    {
        await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        var resultado = await _casoDeUso.ExecutarAsync(ComandoPf(email: "LUCAS@exemplo.com.br", documento: "12345678909"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.EmailJaCadastrado, Assert.Single(resultado.Erros));
    }

    [Fact]
    public async Task Executar_DocumentoJaExistente_DocumentoJaCadastrado()
    {
        await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        var resultado = await _casoDeUso.ExecutarAsync(ComandoPf(email: "outro@exemplo.com.br", documento: "529.982.247-25"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.DocumentoJaCadastrado, Assert.Single(resultado.Erros));
    }

    [Theory]
    [InlineData(ResultadoPersistencia.EmailDuplicado, "email-ja-cadastrado")]
    [InlineData(ResultadoPersistencia.DocumentoDuplicado, "documento-ja-cadastrado")]
    public async Task Executar_ViolacaoDeUnicidadeNaBase_TraduzParaConflito(ResultadoPersistencia persistencia, string codigo)
    {
        _repositorio.ResultadoForcado = persistencia;

        var resultado = await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(codigo, erro.Codigo);
        Assert.Equal(TipoErro.Conflito, erro.Tipo);
        Assert.Empty(_metricas.Medicoes);
    }

    [Fact]
    public async Task Executar_SenhaNuncaChegaAoRepositorioEmTexto()
    {
        await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        Assert.Equal("hash:SenhaForte@123", _repositorio.Usuarios[0].SenhaHash);
        Assert.NotEqual("SenhaForte@123", _repositorio.Usuarios[0].SenhaHash);
    }

    [Fact]
    public async Task Executar_Sucesso_IncrementaMetricaComTipoConta()
    {
        await _casoDeUso.ExecutarAsync(ComandoPf(), CancellationToken.None);

        var medicao = Assert.Single(_metricas.Medicoes);
        Assert.Equal("bank.usuarios.cadastros", medicao.Nome);
        Assert.Equal(1, medicao.Valor);
        Assert.Equal("PessoaFisica", medicao.Tags["tipo_conta"]);
    }

    private static RegistrarUsuarioComando ComandoPf(string email = "Lucas@Exemplo.com.br", string documento = "529.982.247-25") =>
        new("Lucas Silva", email, documento, "PessoaFisica", "SenhaForte@123");
}
