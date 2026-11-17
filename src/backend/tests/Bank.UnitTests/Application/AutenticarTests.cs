using Bank.Application.Abstracoes;
using Bank.Application.Autenticacao;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

public sealed class AutenticarTests : IDisposable
{
    private readonly RepositorioFalso _repositorio = new();
    private readonly HasherFalso _hasher = new();
    private readonly HashSenhaFicticio _hashFicticio;
    private readonly MetricasCapturadas _metricas = new();
    private readonly Autenticar _casoDeUso;
    private readonly Usuario _usuario;

    public AutenticarTests()
    {
        _hashFicticio = new HashSenhaFicticio(_hasher);
        _casoDeUso = new Autenticar(_repositorio, _hasher, _hashFicticio, new EmissorFalso(), _metricas.Metricas, NullLogger<Autenticar>.Instance);
        _usuario = Usuario.Criar(
            "Lucas Silva",
            Email.Criar("lucas@exemplo.com.br").Valor,
            Documento.Criar("52998224725", TipoConta.PessoaFisica).Valor,
            _hasher.GerarHash("SenhaForte@123"),
            10m,
            DateTimeOffset.UtcNow);
        _repositorio.Usuarios.Add(_usuario);
    }

    public void Dispose() => _metricas.Dispose();

    [Theory]
    [InlineData("lucas@exemplo.com.br")]
    [InlineData("  LUCAS@Exemplo.com.br ")]
    public async Task Executar_CredenciaisCorretas_EmiteToken(string email)
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando(email, "SenhaForte@123"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("token-de:" + _usuario.Id, resultado.Valor.Token);
        Assert.Equal("Bearer", resultado.Valor.TipoToken);
        Assert.Equal(3600, resultado.Valor.ExpiraEmSegundos);
        AssertMetricaLogin("sucesso");
    }

    [Fact]
    public async Task Executar_SenhaErrada_CredenciaisInvalidas()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("lucas@exemplo.com.br", "Errada@123"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.CredenciaisInvalidas, Assert.Single(resultado.Erros));
        AssertMetricaLogin("falha");
    }

    [Fact]
    public async Task Executar_EmailInexistente_VerificaHashFicticioEDevolveOMesmoErro()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("ninguem@exemplo.com.br", "SenhaForte@123"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.CredenciaisInvalidas, Assert.Single(resultado.Erros));
        Assert.Equal(_hashFicticio.Valor, Assert.Single(_hasher.HashesVerificados));
        AssertMetricaLogin("falha");
    }

    [Fact]
    public async Task Executar_RehashNecessario_RegravaOHash()
    {
        _hasher.ResultadoForcado = ResultadoVerificacaoSenha.SucessoRehashNecessario;

        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("lucas@exemplo.com.br", "SenhaForte@123"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, _repositorio.Atualizacoes);
    }

    [Theory]
    [InlineData(null, "SenhaForte@123", "email")]
    [InlineData(" ", "SenhaForte@123", "email")]
    [InlineData("lucas@exemplo.com.br", "", "senha")]
    [InlineData("lucas@exemplo.com.br", null, "senha")]
    public async Task Executar_CampoAusente_ErroDeValidacao(string? email, string? senha, string campo)
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando(email, senha), CancellationToken.None);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(TipoErro.Validacao, erro.Tipo);
        Assert.Equal(campo, erro.Campo);
        Assert.Empty(_hasher.HashesVerificados);
    }

    private void AssertMetricaLogin(string resultado)
    {
        var medicao = Assert.Single(_metricas.Medicoes);
        Assert.Equal("bank.autenticacao.logins", medicao.Nome);
        Assert.Equal(resultado, medicao.Tags["resultado"]);
    }
}
