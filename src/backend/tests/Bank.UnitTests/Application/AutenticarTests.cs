using Bank.Application.Abstracoes;
using Bank.Application.Autenticacao;
using Bank.Domain.Comum;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

public sealed class AutenticarTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly RepositorioFalso _repositorio = new();
    private readonly HasherFalso _hasher = new();
    private readonly HashSenhaFicticio _hashFicticio;
    private readonly MetricasCapturadas _metricas = new();
    private readonly SessoesFalsas _sessoes = new();
    private readonly GeradorFalso _gerador = new();
    private readonly UnidadeFalsa _unidade = new();
    private readonly Autenticar _casoDeUso;
    private readonly Usuario _usuario;

    public AutenticarTests()
    {
        _hashFicticio = new HashSenhaFicticio(_hasher);
        _casoDeUso = new Autenticar(
            _repositorio, _hasher, _hashFicticio, new EmissorFalso(), _sessoes, _gerador, _unidade,
            OpcoesSessao.Monitor(), new RelogioFixo(Agora), _metricas.Metricas, NullLogger<Autenticar>.Instance);
        _usuario = Usuario.Criar(
            "Lucas Silva",
            Email.Criar("lucas@exemplo.com.br").Valor,
            Documento.Criar("52998224725", TipoConta.PessoaFisica).Valor,
            _hasher.GerarHash("SenhaForte@123"),
            ContasDeTeste.Corrente(),
            10m,
            DateTimeOffset.UtcNow);
        _repositorio.Usuarios.Add(_usuario);
    }

    public void Dispose() => _metricas.Dispose();

    [Theory]
    [InlineData("lucas@exemplo.com.br")]
    [InlineData("  LUCAS@Exemplo.com.br ")]
    public async Task Executar_CredenciaisCorretas_CriaSessaoEEmiteTokenECredencial(string email)
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando(email, "SenhaForte@123"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        var sessao = Assert.Single(_sessoes.Sessoes);
        Assert.Equal(_usuario.Id, sessao.UsuarioId);
        Assert.Equal(Agora, sessao.IniciadaEm);
        Assert.Equal("token-de:" + _usuario.Id + ":" + sessao.Id, resultado.Valor.Token);
        Assert.Equal("Bearer", resultado.Valor.TipoToken);
        Assert.Equal(300, resultado.Valor.ExpiraEmSegundos);
        Assert.Equal("cred-1", resultado.Valor.Credencial);
        Assert.Equal(GeradorFalso.Hash("cred-1"), sessao.CredencialAtiva.Hash);
        Assert.Equal(900, resultado.Valor.CredencialExpiraEmSegundos);
        Assert.Equal(900, resultado.Valor.SessaoExpiraPorInatividadeEmSegundos);
        Assert.Equal(28800, resultado.Valor.SessaoExpiraEmSegundos);
        Assert.Equal((1, 1, 0), (_unidade.Inicios, _unidade.Confirmacoes, _unidade.Desfeitas));
        AssertMetricaLogin("sucesso");
    }

    [Fact]
    public async Task Executar_ComCredencialDeSessaoAtiva_RevogaAAnteriorPorNovoLogin()
    {
        var anterior = Sessao.Iniciar(_usuario.Id, GeradorFalso.Hash("antiga"), Agora.AddMinutes(-5), OpcoesSessao.Padrao().Limites());
        _sessoes.Sessoes.Add(anterior);

        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("lucas@exemplo.com.br", "SenhaForte@123", "antiga"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(MotivoRevogacao.NovoLogin, anterior.MotivoRevogacao);
        Assert.Equal(Agora, anterior.RevogadaEm);
        Assert.Equal(2, _sessoes.Sessoes.Count);
        var revogacao = Assert.Single(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.sessoes_revogadas");
        Assert.Equal("novo-login", revogacao.Tags["motivo"]);
    }

    [Fact]
    public async Task Executar_ComCredencialDesconhecida_Ignora()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("lucas@exemplo.com.br", "SenhaForte@123", "inventada"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(_sessoes.Sessoes);
        Assert.DoesNotContain(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.sessoes_revogadas");
    }

    [Fact]
    public async Task Executar_SenhaErrada_CredenciaisInvalidas_SemSessaoNemRevogacao()
    {
        var anterior = Sessao.Iniciar(_usuario.Id, GeradorFalso.Hash("antiga"), Agora, OpcoesSessao.Padrao().Limites());
        _sessoes.Sessoes.Add(anterior);

        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("lucas@exemplo.com.br", "Errada@123", "antiga"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.CredenciaisInvalidas, Assert.Single(resultado.Erros));
        Assert.Single(_sessoes.Sessoes);
        Assert.False(anterior.Revogada);
        Assert.Equal(0, _unidade.Inicios);
        AssertMetricaLogin("falha");
    }

    [Fact]
    public async Task Executar_EmailInexistente_VerificaHashFicticioEDevolveOMesmoErro()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new AutenticarComando("ninguem@exemplo.com.br", "SenhaForte@123"), CancellationToken.None);

        Assert.Equal(ErrosUsuario.CredenciaisInvalidas, Assert.Single(resultado.Erros));
        Assert.Equal(_hashFicticio.Valor, Assert.Single(_hasher.HashesVerificados));
        Assert.Empty(_sessoes.Sessoes);
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
        var medicao = Assert.Single(_metricas.Medicoes, m => m.Nome == "bank.autenticacao.logins");
        Assert.Equal(resultado, medicao.Tags["resultado"]);
    }
}
