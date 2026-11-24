using Bank.Application.Abstracoes;
using Bank.Application.Cartoes;
using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Emissão e consulta do cartão (specs/014-cartao-credito-cobrancas, US1; research R1).</summary>
public sealed class SolicitarCartaoTests : IDisposable
{
    private readonly BancoCartoes _banco = new();
    private readonly LoggerCapturado<SolicitarCartao> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    private SolicitarCartao CasoDeUso() =>
        new(_banco, _banco, _banco.Dados, _banco, new MonitorDeOpcoes<CartoesOptions>(_banco.Opcoes), _banco.Calendario, _banco.Relogio, _banco.MetricasCartoes, _logger);

    [Fact]
    public async Task Solicitar_GravaComLimiteDaConfiguracao()
    {
        var pf = _banco.CriarPf();
        _banco.Opcoes.LimitePadrao = 1234.56m;

        var resultado = await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(pf.Id, "002"), Ct);

        var cartao = Assert.Single(_banco.Cartoes);
        Assert.Same(cartao, resultado.Valor.Cartao);
        Assert.Equal(("002", 1234.56m, "LUCAS SILVA", "11/31", 0), (cartao.CodigoBandeira, cartao.LimiteTotal, cartao.NomeImpresso, cartao.Validade.ToString(), cartao.Variacao));
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.cartoes.emitidos" && m.Tags["bandeira"] == "002");
        Assert.DoesNotContain(_logger.Registros, r => r.Mensagem.Contains(_banco.Dados.Recuperar(cartao).Numero.Valor, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("003")]
    public async Task BandeiraInvalida_ErroNoCampo(string? bandeira)
    {
        var resultado = await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(_banco.CriarPf().Id, bandeira), Ct);

        Assert.Equal(SolicitarCartao.CampoBandeira, Assert.Single(resultado.Erros).Campo);
        Assert.Empty(_banco.Cartoes);
    }

    [Fact]
    public async Task JaTemCartao_OuCorridaNoIndice_MesmoErro()
    {
        var (pf, _, _) = _banco.CriarPfComCartao();
        Assert.Equal(ErrosCartao.JaExistente, Assert.Single((await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(pf.Id, "001"), Ct)).Erros));

        var outra = _banco.CriarPf();
        _banco.Confirmacoes.Enqueue(ResultadoGravacao.CartaoDoUsuarioDuplicado);
        Assert.Equal(ErrosCartao.JaExistente, Assert.Single((await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(outra.Id, "001"), Ct)).Erros));
        Assert.Single(_banco.Cartoes);
    }

    [Fact]
    public async Task NumeroDuplicado_TentaAProximaVariacao()
    {
        var pf = _banco.CriarPf();
        _banco.Confirmacoes.Enqueue(ResultadoGravacao.NumeroDeCartaoDuplicado);
        _banco.Confirmacoes.Enqueue(ResultadoGravacao.NumeroDeCartaoDuplicado);

        var resultado = await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(pf.Id, "001"), Ct);

        Assert.Equal(2, resultado.Valor.Cartao.Variacao);
        Assert.Single(_banco.Cartoes);
    }

    [Fact]
    public async Task CincoColisoes_FalhaInesperada()
    {
        var pf = _banco.CriarPf();
        for (var i = 0; i < 5; i++)
        {
            _banco.Confirmacoes.Enqueue(ResultadoGravacao.NumeroDeCartaoDuplicado);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(pf.Id, "001"), Ct));
    }

    [Fact]
    public async Task UsuarioInexistente_Erro() =>
        Assert.Equal(ErrosUsuario.NaoEncontrado, Assert.Single((await CasoDeUso().ExecutarAsync(new SolicitarCartaoComando(UsuarioId.Novo(CartoesDeTeste.Agora), "001"), Ct)).Erros));

    [Fact]
    public async Task ObterEExibir_SoComCartao()
    {
        var semCartao = _banco.CriarPf();
        var (pf, cartao, dados) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, _banco.CriarPj(), 10m);

        Assert.Equal(ErrosCartao.NaoEncontrado, Assert.Single((await new ObterMeuCartao(_banco).ExecutarAsync(semCartao.Id, Ct)).Erros));
        Assert.Equal(ErrosCartao.NaoEncontrado, Assert.Single((await new ExibirDadosCartao(_banco, _banco.Dados).ExecutarAsync(semCartao.Id, Ct)).Erros));

        var meu = (await new ObterMeuCartao(_banco).ExecutarAsync(pf.Id, Ct)).Valor;
        Assert.Equal((4990m, 10m), (meu.Cartao.LimiteDisponivel, meu.ComprasALancar));
        var exibidos = (await new ExibirDadosCartao(_banco, _banco.Dados).ExecutarAsync(pf.Id, Ct)).Valor;
        Assert.Equal((dados.Numero.Valor, "123"), (exibidos.Numero.Valor, exibidos.Cvv.Valor));
        Assert.DoesNotContain(dados.Numero.Valor, exibidos.ToString(), StringComparison.Ordinal);
    }
}
