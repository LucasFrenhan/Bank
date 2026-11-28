using Bank.Application.Abstracoes;
using Bank.Application.Cobrancas;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;

namespace Bank.UnitTests.Application;

/// <summary>Pagamento com cartão (specs/014-cartao-credito-cobrancas, US3; FR-014 a FR-021; research R3, R6, R10, R11).</summary>
public sealed class PagarCobrancaTests : IDisposable
{
    private readonly BancoCartoes _banco = new();
    private readonly LoggerCapturado<PagarCobranca> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _banco.Dispose();

    private static PagarCobrancaComando Comando(Cartao cartao, DadosDerivados dados, Cobranca cobranca, Guid? chave = null, string? cvv = null) =>
        new(cartao.UsuarioId, cobranca.Id.ToString(), (chave ?? Guid.NewGuid()).ToString(), "LUCAS", dados.Numero.Valor, cartao.Validade.ToString(), cvv ?? dados.Cvv.Valor);

    private Task<Resultado<CobrancaPagaComCartao>> PagarAsync(PagarCobrancaComando comando) => _banco.PagarCobranca(_logger).ExecutarAsync(comando, Ct);

    [Fact]
    public async Task Aprovado_GravaTudo_ENaoMexeEmSaldo()
    {
        var pj = _banco.CriarPj();
        var (pf, cartao, dados) = _banco.CriarPfComCartao();
        var cobranca = _banco.CriarCobranca(pj);

        var resultado = (await PagarAsync(Comando(cartao, dados, cobranca))).Valor;

        Assert.False(resultado.Replay);
        Assert.Equal((cobranca.Id, 299.90m, "001", "Cafeteria Ltda"), (resultado.Pagamento.CobrancaId, resultado.Pagamento.Valor, resultado.Pagamento.CodigoBandeira, resultado.Pagamento.LojistaNome));
        Assert.Equal(StatusCobranca.Paga, cobranca.Status);
        Assert.Equal(4700.10m, cartao.LimiteDisponivel);
        Assert.Equal((5000m, 0m), (pf.Carteira.Saldo, pj.Carteira.Saldo));
        Assert.Single(_banco.Pagamentos);
        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(_banco.Faturas).Competencia);
        Assert.Contains(_banco.Medicoes, m => m.Nome == "bank.cobrancas.autorizacoes" && m.Tags["resultado"] == "aprovada" && m.Tags["bandeira"] == "001");
    }

    [Fact]
    public async Task Validacao_AcumulaTodosOsCampos()
    {
        var resultado = await PagarAsync(new PagarCobrancaComando(_banco.CriarPf().Id, "x", null, " ", "1", "99", "1"));

        Assert.Equal([Entradas.CampoChave, "cobrancaId", "nomeTitularCartao", "numeroCartao", "validade", "cvv"], resultado.Erros.Select(e => e.Campo));
    }

    [Fact]
    public async Task CartaoVencido_ErroDeValidacao()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        _banco.Relogio.Agora = new DateTimeOffset(2031, 12, 1, 3, 0, 0, TimeSpan.Zero); // 00:00 de Brasília

        Assert.Equal("validade", Assert.Single((await PagarAsync(Comando(cartao, dados, cobranca))).Erros).Campo);
        Assert.Empty(_banco.Autorizacoes);
    }

    [Fact]
    public async Task DadosErrados_OuSemCartao_MesmoErro_ERecusaGravada()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var semCartao = _banco.CriarPf();
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        var comandos = new[]
        {
            Comando(cartao, dados, cobranca, cvv: "999"),
            Comando(cartao, dados, cobranca) with { NumeroCartao = CartoesDeTeste.Numero.Valor },
            Comando(cartao, dados, cobranca) with { Validade = "12/99" },
            Comando(cartao, dados, cobranca) with { PagadorUsuarioId = semCartao.Id },
        };

        foreach (var comando in comandos)
        {
            Assert.Equal(ErrosCartao.NaoAutorizado, Assert.Single((await PagarAsync(comando)).Erros));
        }

        Assert.Equal(4, _banco.Autorizacoes.Count);
        Assert.All(_banco.Autorizacoes, a => Assert.Equal((MotivoRecusa.CartaoNaoAutorizado, null), (a.Motivo, a.CartaoId)));
        Assert.Equal((StatusCobranca.Pendente, MotivoRecusa.CartaoNaoAutorizado), (cobranca.Status, cobranca.UltimaRecusaMotivo));
        Assert.Equal(5000m, cartao.LimiteDisponivel);
    }

    [Fact]
    public async Task LimiteInsuficiente_Recusa_ENaoAbreFatura()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao(limite: 100m);
        var cobranca = _banco.CriarCobranca(_banco.CriarPj(), 100.01m);

        Assert.Equal(ErrosCartao.LimiteInsuficiente, Assert.Single((await PagarAsync(Comando(cartao, dados, cobranca))).Erros));
        Assert.Empty(_banco.Faturas);
        Assert.Equal(cartao.Id, Assert.Single(_banco.Autorizacoes).CartaoId);
        Assert.Contains(_banco.Medicoes, m => m.Tags.GetValueOrDefault("resultado") == "limite-cartao-insuficiente");
    }

    [Fact]
    public async Task FaturaVencida_Recusa_AntesDoLimite()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, dados) = _banco.CriarPfComCartao(limite: 100m);
        _banco.Comprar(cartao, pj, 100m);
        var fatura = _banco.Faturas[0];
        fatura.Lancar(_banco.Pagamentos[0].Compra, _banco.Relogio.Agora);
        fatura.Liberar(new DateOnly(2026, 12, 8), _banco.Relogio.Agora);
        _banco.Relogio.Agora = new DateTimeOffset(2026, 12, 11, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(ErrosCartao.BloqueadoPorFaturaVencida, Assert.Single((await PagarAsync(Comando(cartao, dados, _banco.CriarCobranca(pj)))).Erros));
    }

    [Fact]
    public async Task Inexistente_404_ENaoPendente_409()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var cancelada = _banco.CriarCobranca(pj);
        cancelada.Cancelar(CartoesDeTeste.Agora);

        Assert.Equal(ErrosCobranca.NaoEncontrada, Assert.Single((await PagarAsync(Comando(cartao, dados, cancelada) with { CobrancaId = Guid.NewGuid().ToString() })).Erros));
        Assert.Equal(ErrosCobranca.NaoPendente, Assert.Single((await PagarAsync(Comando(cartao, dados, cancelada))).Erros));
        Assert.Empty(_banco.Autorizacoes);
        Assert.Equal(2, _banco.Desfeitas);
    }

    [Fact]
    public async Task MesmaChave_Replay_EDivergencias()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var (cobranca, outra) = (_banco.CriarCobranca(pj), _banco.CriarCobranca(pj));
        var chave = Guid.NewGuid();
        var primeiro = (await PagarAsync(Comando(cartao, dados, cobranca, chave))).Valor;

        var repetido = (await PagarAsync(Comando(cartao, dados, cobranca, chave))).Valor;

        Assert.True(repetido.Replay);
        Assert.Equal(primeiro.Pagamento, repetido.Pagamento);
        Assert.Equal(ErrosIdempotencia.Divergente, Assert.Single((await PagarAsync(Comando(cartao, dados, outra, chave))).Erros));
        Assert.Equal(ErrosIdempotencia.Divergente, Assert.Single((await PagarAsync(Comando(cartao, dados, cobranca, chave, cvv: "999"))).Erros));
        Assert.Equal(4700.10m, cartao.LimiteDisponivel);
    }

    [Fact]
    public async Task RecusaNaoConsomeAChave()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        var chave = Guid.NewGuid();
        await PagarAsync(Comando(cartao, dados, cobranca, chave, cvv: "999"));

        Assert.True((await PagarAsync(Comando(cartao, dados, cobranca, chave))).Sucesso);
    }

    [Fact]
    public async Task OutraRequisicaoComAMesmaChave_VistaDepoisDoBloqueio_OuNaConfirmacao()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var (a, b) = (_banco.CriarCobranca(pj, 1m), _banco.CriarCobranca(pj, 1m));
        var (chaveA, chaveB) = (Guid.NewGuid(), Guid.NewGuid());

        _banco.AoBloquear = () =>
        {
            _banco.AoBloquear = null;
            _banco.Pagamentos.Add(PagamentoCobranca.Executar(a, cartao, CartoesDeTeste.Fatura(cartao), "Cafeteria Ltda", CartoesDeTeste.Hoje, chaveA, CartoesDeTeste.Agora).Valor);
        };
        Assert.True((await PagarAsync(Comando(cartao, dados, a, chaveA))).Valor.Replay);

        _banco.Confirmacoes.Enqueue(ResultadoGravacao.ChaveDuplicada);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PagarAsync(Comando(cartao, dados, b, chaveB)));
    }

    [Fact]
    public async Task FaturaDoCicloJaLiberada_CompraVaiParaOCicloSeguinte()
    {
        var pj = _banco.CriarPj();
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        _banco.Comprar(cartao, pj, 1m);
        _banco.Faturas[0].Lancar(_banco.Pagamentos[0].Compra, _banco.Relogio.Agora);
        _banco.Faturas[0].Liberar(new DateOnly(2026, 12, 8), _banco.Relogio.Agora);

        await PagarAsync(Comando(cartao, dados, _banco.CriarCobranca(pj))); // relógio ainda em 24/11

        var compra = _banco.Pagamentos[^1].Compra;
        Assert.Equal((new DateOnly(2026, 12, 8), CartoesDeTeste.Agora), (compra.DataCompra, compra.AutorizadaEm));
        Assert.Equal(new DateOnly(2027, 1, 1), _banco.Faturas[^1].Competencia);
    }

    [Fact]
    public async Task NadaVazaNoTextoDoComandoNemNosLogs()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        var comando = Comando(cartao, dados, cobranca);
        await PagarAsync(comando with { Cvv = "987" });
        await PagarAsync(comando);

        Assert.DoesNotContain(dados.Numero.Valor, comando.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Cvv", comando.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, _logger.Registros.Count);
        Assert.All(_logger.Registros, r =>
        {
            Assert.DoesNotContain(dados.Numero.Valor, r.Mensagem, StringComparison.Ordinal);
            Assert.DoesNotContain(r.Propriedades.Values, v => Equals(v, "987") || Equals(v, dados.Numero.Valor));
        });
    }

    [Fact]
    public async Task FalhaInesperada_Desfaz()
    {
        var (_, cartao, dados) = _banco.CriarPfComCartao();
        var cobranca = _banco.CriarCobranca(_banco.CriarPj());
        _banco.FalharAoBloquear = cartao.Id.Valor;

        await Assert.ThrowsAsync<InvalidOperationException>(() => PagarAsync(Comando(cartao, dados, cobranca)));

        Assert.Equal(1, _banco.Desfeitas);
        Assert.Equal(StatusCobranca.Pendente, cobranca.Status);
    }
}
