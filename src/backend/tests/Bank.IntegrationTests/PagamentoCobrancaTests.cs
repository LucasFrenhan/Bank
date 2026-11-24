using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Pagamento com o cartão (specs/014-cartao-credito-cobrancas, US3; FR-014 a FR-021; SC-002).</summary>
public class PagamentoCobrancaTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>, IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Aprovado_PagaConsomeLimiteESaldosNaoMudam()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj);

        using var resposta = await PagarAsync(client, cartao, cobranca);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(
            ["cobrancaId", "status", "valor", "bandeira", "ultimosDigitosCartao", "codigoAutorizacao", "lojistaNome", "pagaEm"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal((cobranca, "Paga", "299.90", cartao.UltimosDigitos, "Empresa Exemplo Ltda"),
            (json.GetProperty("cobrancaId").GetGuid(), json.GetProperty("status").GetString(), json.GetProperty("valor").GetRawText(),
             json.GetProperty("ultimosDigitosCartao").GetString(), json.GetProperty("lojistaNome").GetString()));
        Assert.Matches("^[A-Z0-9]{6}$", json.GetProperty("codigoAutorizacao").GetString());

        var meu = await GetOkAsync(client, cartao.Conta.Token, MeuCartao);
        Assert.Equal((4700.10m, 299.90m), (meu.GetProperty("limiteDisponivel").GetDecimal(), meu.GetProperty("comprasALancar").GetDecimal()));
        Assert.Equal(5000m, await SaldoAsync(client, cartao.Conta));
        Assert.Equal(0m, await SaldoAsync(client, pj));
        var recebivel = await RecebivelDaCobrancaAsync(client, pj, cobranca);
        Assert.Equal(("Pendente", 299.90m), (recebivel.GetProperty("situacao").GetString(), recebivel.GetProperty("valorLiquido").GetDecimal()));
    }

    [Fact]
    public async Task Aprovado_PublicaUmEventoNoOutbox()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var chave = Guid.NewGuid();
        var cobranca = await EmitirAsync(client, pj);
        (await PagarAsync(client, cartao, cobranca, chave)).Dispose();
        (await PagarAsync(client, cartao, cobranca, chave)).Dispose();

        await using var contexto = factory.CriarContexto();
        var eventos = await contexto.EventosOutbox.Where(e => e.Tipo == "cobranca.paga").ToListAsync(Ct);
        var evento = Assert.Single(eventos, e => e.Payload.Contains(cobranca.ToString(), StringComparison.OrdinalIgnoreCase));
        using var payload = JsonDocument.Parse(evento.Payload);
        var raiz = payload.RootElement;
        Assert.Equal(
            ["cobrancaId", "eventoId", "lojistaNome", "lojistaUsuarioId", "ocorridoEm", "pagadorUsuarioId", "tipoEvento", "ultimosDigitosCartao", "valor", "versao"],
            raiz.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((evento.Id.Valor, 1, "CobrancaPaga", cartao.UltimosDigitos, 299.90m, "Empresa Exemplo Ltda"),
            (raiz.GetProperty("eventoId").GetGuid(), raiz.GetProperty("versao").GetInt32(), raiz.GetProperty("tipoEvento").GetString(),
             raiz.GetProperty("ultimosDigitosCartao").GetString(), raiz.GetProperty("valor").GetDecimal(), raiz.GetProperty("lojistaNome").GetString()));
    }

    [Fact]
    public async Task Limite_EhInclusivo_EAcimaRecusaSemMudarNada()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        await ComprarAsync(client, cartao, pj, 299.90m);
        var acima = await EmitirAsync(client, pj, 4700.11m);

        using (var recusa = await PagarAsync(client, cartao, acima))
        {
            await ApiBank.AssertProblemaAsync(recusa, HttpStatusCode.UnprocessableEntity, Tipo("limite-cartao-insuficiente"));
        }

        Assert.Equal(4700.10m, await LimiteDisponivelAsync(client, cartao));
        var json = await GetOkAsync(client, pj.Token, $"{Cobrancas}/{acima}");
        Assert.Equal(("Pendente", "LimiteInsuficiente"), (json.GetProperty("status").GetString(), json.GetProperty("ultimaRecusa").GetProperty("motivo").GetString()));

        await ComprarAsync(client, cartao, pj, 4700.10m);
        Assert.Equal(0m, await LimiteDisponivelAsync(client, cartao));

        // Outra PF com limite paga a cobrança recusada.
        var outro = await CriarPfComCartaoAsync(factory, client, "002");
        using var aprovada = await PagarAsync(client, outro, acima);
        Assert.Equal(HttpStatusCode.OK, aprovada.StatusCode);
    }

    public static TheoryData<string, object> CamposInvalidos() => new()
    {
        { "numeroCartao", new { nomeTitularCartao = "X", numeroCartao = "4000001234567890", validade = "11/31", cvv = "123" } },
        { "numeroCartao", new { nomeTitularCartao = "X", numeroCartao = "123", validade = "11/31", cvv = "123" } },
        { "validade", new { nomeTitularCartao = "X", numeroCartao = "4000001234567899", validade = "13/31", cvv = "123" } },
        { "validade", new { nomeTitularCartao = "X", numeroCartao = "4000001234567899", validade = "01/20", cvv = "123" } },
        { "cvv", new { nomeTitularCartao = "X", numeroCartao = "4000001234567899", validade = "11/31", cvv = "1234" } },
        { "cvv", new { nomeTitularCartao = "X", numeroCartao = "4000001234567899", validade = "11/31", cvv = "12" } },
        { "nomeTitularCartao", new { nomeTitularCartao = " ", numeroCartao = "4000001234567899", validade = "11/31", cvv = "123" } },
    };

    [Theory]
    [MemberData(nameof(CamposInvalidos))]
    public async Task EstruturaInvalida_400_SemTentativaNaBandeira(string campo, object corpo)
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj);

        using var resposta = await PagarAsync(client, cartao, cobranca, corpo: corpo);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.Equal(campo, Assert.Single(json.GetProperty("errors").EnumerateObject()).Name);
        Assert.DoesNotContain("4000001234567890", json.GetRawText(), StringComparison.Ordinal);
        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.AutorizacoesCartao.AnyAsync(a => a.CobrancaId == CobrancaId.De(cobranca), Ct));
    }

    [Fact]
    public async Task EstruturaInvalida_TodosOsCamposDeUmaVez()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var resposta = await PostAsync(client, pf.Token, $"{Cobrancas}/abc/pagar", new { });

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.Equal(
            ["Idempotency-Key", "cobrancaId", "cvv", "nomeTitularCartao", "numeroCartao", "validade"],
            json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DadosQueNaoSaoDoProprioCartao_MesmoErro()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var alheio = await CriarPfComCartaoAsync(factory, client, "002");
        var semCartao = await CriarPfAsync(client);
        var cobranca = await EmitirAsync(client, pj);

        var tentativas = new[]
        {
            await PagarAsync(client, cartao, cobranca, corpo: alheio.Corpo()),
            await PagarAsync(client, cartao, cobranca, corpo: cartao.Corpo(numero: "4000001234567899")),
            await PagarAsync(client, cartao, cobranca, corpo: cartao.Corpo(cvv: cartao.Cvv == "000" ? "001" : "000")),
            await PagarAsync(client, cartao, cobranca, corpo: cartao.Corpo(validade: "12/99")),
            await PagarAsync(client, new CartaoTeste(semCartao, "3000001234567890", "12/99", "123"), cobranca, corpo: cartao.Corpo()),
            await PagarAsync(client, cartao, cobranca, corpo: cartao.Corpo(numero: NumeroCartao.DePrefixo("300000123456789").Valor)),
        };

        var detalhes = new HashSet<string?>();
        foreach (var tentativa in tentativas)
        {
            var json = await ApiBank.AssertProblemaAsync(tentativa, HttpStatusCode.UnprocessableEntity, Tipo("cartao-nao-autorizado"));
            detalhes.Add(json.GetProperty("detail").GetString());
            tentativa.Dispose();
        }

        Assert.Single(detalhes);
        Assert.Equal(5000m, await LimiteDisponivelAsync(client, cartao));
        await using var contexto = factory.CriarContexto();
        var registros = await contexto.AutorizacoesCartao.Where(a => a.CobrancaId == CobrancaId.De(cobranca)).ToListAsync(Ct);
        Assert.Equal(tentativas.Length, registros.Count);
        Assert.All(registros, r =>
        {
            Assert.Equal((ResultadoAutorizacao.Recusada, MotivoRecusa.CartaoNaoAutorizado), (r.Resultado, r.Motivo));
            Assert.Null(r.CartaoId);
            Assert.Null(r.CodigoAutorizacao);
        });
        Assert.Contains(registros, r => r.CodigoBandeira is null);
        Assert.Contains(registros, r => r.CodigoBandeira == "002");
    }

    [Fact]
    public async Task NomeDoTitular_NaoDecide_ENumeroAceitaEspacos()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj);
        var comEspacos = string.Join(' ', Enumerable.Range(0, 4).Select(i => cartao.Numero.Substring(i * 4, 4)));

        using var resposta = await PagarAsync(client, cartao, cobranca, corpo: cartao.Corpo(numero: comEspacos, titular: "Outro Nome"));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Inexistente_404()
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        using var resposta = await PagarAsync(client, cartao, Guid.NewGuid());

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, Tipo("cobranca-nao-encontrada"));
    }

    [Fact]
    public async Task JaPaga_ComOutraChave_409()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await ComprarAsync(client, cartao, pj, 10m);

        using var resposta = await PagarAsync(client, cartao, cobranca);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, Tipo("cobranca-nao-pendente"));
        Assert.Equal(4990m, await LimiteDisponivelAsync(client, cartao));
    }

    [Fact]
    public async Task MesmaChave_DevolveOOriginal_EOutrosDadosDivergem()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var (cobranca, outra) = (await EmitirAsync(client, pj), await EmitirAsync(client, pj));
        var chave = Guid.NewGuid();

        using var primeira = await PagarAsync(client, cartao, cobranca, chave);
        using var repetida = await PagarAsync(client, cartao, cobranca, chave);
        using var outraCobranca = await PagarAsync(client, cartao, outra, chave);
        using var outrosDados = await PagarAsync(client, cartao, cobranca, chave, cartao.Corpo(cvv: cartao.Cvv == "000" ? "001" : "000"));

        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(await primeira.Content.ReadAsStringAsync(Ct), await repetida.Content.ReadAsStringAsync(Ct));
        await ApiBank.AssertProblemaAsync(outraCobranca, HttpStatusCode.Conflict, Tipo("idempotencia-divergente"));
        await ApiBank.AssertProblemaAsync(outrosDados, HttpStatusCode.Conflict, Tipo("idempotencia-divergente"));
        Assert.Equal(4700.10m, await LimiteDisponivelAsync(client, cartao));
    }

    [Fact]
    public async Task Recusa_NaoConsomeAChave_EAChaveEhPorPagador()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var (a, b) = (await CriarPfComCartaoAsync(factory, client), await CriarPfComCartaoAsync(factory, client));
        var (cobrancaA, cobrancaB) = (await EmitirAsync(client, pj), await EmitirAsync(client, pj));
        var chave = Guid.NewGuid();

        using var recusada = await PagarAsync(client, a, cobrancaA, chave, a.Corpo(cvv: a.Cvv == "000" ? "001" : "000"));
        using var aprovada = await PagarAsync(client, a, cobrancaA, chave);
        using var deOutro = await PagarAsync(client, b, cobrancaB, chave);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, recusada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aprovada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deOutro.StatusCode);
    }

    [Fact]
    public async Task CartaoVencido_400()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj);
        var validade = ValidadeCartao.Criar(cartao.Validade).Valor;
        var ultimoDia = new DateOnly(validade.Ano, validade.Mes, 1).AddMonths(1).AddDays(-1);

        factory.Relogio.Agora = Brasilia(ultimoDia.Year, ultimoDia.Month, ultimoDia.Day, 23, 59, 59).AddSeconds(1);
        using (var vencido = await PagarAsync(client, cartao, cobranca))
        {
            var json = await ApiBank.AssertProblemaAsync(vencido, HttpStatusCode.BadRequest, Tipo("validacao"));
            Assert.True(json.GetProperty("errors").TryGetProperty("validade", out _));
        }

        factory.Relogio.Agora = Brasilia(ultimoDia.Year, ultimoDia.Month, ultimoDia.Day, 23, 59, 59);
        using var noUltimoInstante = await PagarAsync(client, cartao, cobranca);
        Assert.Equal(HttpStatusCode.OK, noUltimoInstante.StatusCode);
    }

    [Fact]
    public async Task FaturaVencida_Bloqueia_AteSerPaga()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        factory.Relogio.Agora = Brasilia(2028, 3, 1);
        await ComprarAsync(client, cartao, pj, 100m);
        await factory.ProcessarEmAsync(Brasilia(2028, 3, 8, 0, 0, 0));

        // No dia do vencimento (sexta-feira 10/03/2028) não bloqueia.
        factory.Relogio.Agora = Brasilia(2028, 3, 10, 23, 59, 59);
        await ComprarAsync(client, cartao, pj, 1m);

        factory.Relogio.Agora = Brasilia(2028, 3, 11, 0, 0, 0);
        var cobranca = await EmitirAsync(client, pj, 10m);
        using (var bloqueada = await PagarAsync(client, cartao, cobranca))
        {
            await ApiBank.AssertProblemaAsync(bloqueada, HttpStatusCode.UnprocessableEntity, Tipo("cartao-bloqueado-fatura-vencida"));
        }

        Assert.Equal(4899m, await LimiteDisponivelAsync(client, cartao));
        Assert.Equal("FaturaVencida", (await GetOkAsync(client, pj.Token, $"{Cobrancas}/{cobranca}")).GetProperty("ultimaRecusa").GetProperty("motivo").GetString());

        // Vencida e sem limite: a recusa é por fatura vencida.
        var semLimite = await EmitirAsync(client, pj, 5000.01m);
        using (var recusa = await PagarAsync(client, cartao, semLimite))
        {
            await ApiBank.AssertProblemaAsync(recusa, HttpStatusCode.UnprocessableEntity, Tipo("cartao-bloqueado-fatura-vencida"));
        }

        using (var pagamento = await PagarFaturaAsync(client, cartao, "2028-03"))
        {
            Assert.Equal(HttpStatusCode.OK, pagamento.StatusCode);
        }

        using var aprovada = await PagarAsync(client, cartao, cobranca);
        Assert.Equal(HttpStatusCode.OK, aprovada.StatusCode);
    }

    [Fact]
    public async Task RelogioAtrasado_ComFaturaLiberada_VaiParaOCicloSeguinte()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        factory.Relogio.Agora = Brasilia(2029, 5, 2);
        await ComprarAsync(client, cartao, pj, 10m);
        await factory.ProcessarEmAsync(Brasilia(2029, 5, 8, 0, 0, 0));

        factory.Relogio.Agora = Brasilia(2029, 5, 7, 23, 59, 59);
        var cobranca = await ComprarAsync(client, cartao, pj, 20m);

        await using var contexto = factory.CriarContexto();
        var compra = await contexto.ComprasCartao.SingleAsync(c => c.CobrancaId == CobrancaId.De(cobranca), Ct);
        Assert.Equal((new DateOnly(2029, 5, 8), Brasilia(2029, 5, 7, 23, 59, 59)), (compra.DataCompra, compra.AutorizadaEm));
        Assert.Equal(new DateOnly(2029, 6, 1), await contexto.Faturas.Where(f => f.Id == compra.FaturaId).Select(f => f.Competencia).SingleAsync(Ct));
        Assert.Equal(10m, (await FaturaAsync(client, cartao, "2029-05")).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task P95DoPagamento_AteDoisSegundos()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var tempos = new List<double>();
        for (var i = 0; i < 100; i++)
        {
            var cobranca = await EmitirAsync(client, pj, 1m);
            var inicio = Stopwatch.GetTimestamp();
            using var resposta = await PagarAsync(client, cartao, cobranca);
            tempos.Add(Stopwatch.GetElapsedTime(inicio).TotalSeconds);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        tempos.Sort();
        Assert.True(tempos[94] <= 2, $"p95 = {tempos[94]:F2} s");
    }
}
