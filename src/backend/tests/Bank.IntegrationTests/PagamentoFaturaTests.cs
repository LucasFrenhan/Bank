using System.Net;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Pagamento de fatura (specs/014-cartao-credito-cobrancas, US5; FR-028 a FR-029, FR-034).</summary>
public class PagamentoFaturaTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>, IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EmDia_DebitaRegistraNoExtratoELiberaOLimite()
    {
        using var client = factory.CreateClient();
        var cartao = await ComFaturaDisponivelAsync(client, 2027, 299.90m);

        using var resposta = await PagarFaturaAsync(client, cartao, "2027-03");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(("Paga", "299.90", "0.00", 0, "299.90"),
            (json.GetProperty("situacao").GetString(), json.GetProperty("total").GetRawText(), json.GetProperty("juros").GetRawText(),
             json.GetProperty("diasAtraso").GetInt32(), json.GetProperty("valorPago").GetRawText()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("valorAPagar").ValueKind);
        Assert.Equal(4700.10m, await SaldoAsync(client, cartao.Conta));
        Assert.Equal(5000m, await LimiteDisponivelAsync(client, cartao));
        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, cartao.Conta.CarteiraId));

        var extrato = await GetOkAsync(client, cartao.Conta.Token, "/api/v1/transferencias/extrato?direcao=Saida");
        var item = Assert.Single(extrato.GetProperty("itens").EnumerateArray());
        Assert.Equal(("PagamentoFatura", "Saida", 299.90m, "Pagamento da fatura 03/2027"),
            (item.GetProperty("tipo").GetString(), item.GetProperty("direcao").GetString(), item.GetProperty("valor").GetDecimal(), item.GetProperty("descricao").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("transferenciaId").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("contraparte").ValueKind);
    }

    [Fact]
    public async Task Vencida_ComJurosPorDia_GuardadosNoPagamento()
    {
        using var client = factory.CreateClient();
        var cartao = await ComFaturaDisponivelAsync(client, 2028, 1000m); // vence na sexta-feira 10/03/2028
        factory.Relogio.Agora = Brasilia(2028, 3, 20, 10);

        var vencida = await FaturaAsync(client, cartao, "2028-03");
        Assert.Equal(("Vencida", 10, 50m, 1050m),
            (vencida.GetProperty("situacao").GetString(), vencida.GetProperty("diasAtraso").GetInt32(), vencida.GetProperty("juros").GetDecimal(), vencida.GetProperty("valorAPagar").GetDecimal()));

        using (var pagamento = await PagarFaturaAsync(client, cartao, "2028-03"))
        {
            Assert.Equal(HttpStatusCode.OK, pagamento.StatusCode);
        }

        Assert.Equal(3950m, await SaldoAsync(client, cartao.Conta));
        Assert.Equal(5000m, await LimiteDisponivelAsync(client, cartao));

        // O que foi cobrado não muda com outra taxa nem com o passar dos dias.
        factory.Relogio.Agora = Brasilia(2028, 6, 20, 10);
        var paga = await FaturaAsync(client, cartao, "2028-03");
        Assert.Equal(("Paga", 10, 50m, 1050m),
            (paga.GetProperty("situacao").GetString(), paga.GetProperty("diasAtraso").GetInt32(), paga.GetProperty("juros").GetDecimal(), paga.GetProperty("valorPago").GetDecimal()));
        await using var contexto = factory.CriarContexto();
        Assert.Equal(15m, await contexto.Faturas.Where(f => f.Competencia == new DateOnly(2028, 3, 1) && f.PagaEm != null).Select(f => f.TaxaJurosMensal).SingleAsync(Ct));
    }

    [Fact]
    public async Task SaldoInsuficiente_422_NadaMuda()
    {
        using var client = factory.CreateClient();
        var cartao = await ComFaturaDisponivelAsync(client, 2029, 1000m);
        var pj = await CriarLojistaAsync(factory, client);
        for (var i = 0; i < 5; i++)
        {
            factory.Relogio.Agora = Brasilia(2029, 3, 8, 12).AddDays(i);
            (await TransferirAsync(client, cartao.Conta, pj.Documento, 900m)).Dispose();
        }

        factory.Relogio.Agora = Brasilia(2029, 3, 9, 12);
        Assert.Equal(500m, await SaldoAsync(client, cartao.Conta));
        using var resposta = await PagarFaturaAsync(client, cartao, "2029-03");

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.UnprocessableEntity, Tipo("saldo-insuficiente"));
        Assert.Contains("fatura", json.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(500m, await SaldoAsync(client, cartao.Conta));
        Assert.Equal(4000m, await LimiteDisponivelAsync(client, cartao));
        Assert.Equal("Disponivel", (await FaturaAsync(client, cartao, "2029-03")).GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task AbertaFechadaOuPaga_409_EInexistente_404()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        factory.Relogio.Agora = Brasilia(2030, 3, 5, 10);
        await ComprarAsync(client, cartao, pj, 10m);

        await AssertNaoDisponivelAsync("2030-03"); // aberta
        factory.Relogio.Agora = Brasilia(2030, 3, 8, 0, 0, 0);
        await AssertNaoDisponivelAsync("2030-03"); // fechada
        await AssertNaoDisponivelAsync("2030-04"); // ciclo atual sem compras
        await factory.ProcessarAsync();
        (await PagarFaturaAsync(client, cartao, "2030-03")).Dispose();
        await AssertNaoDisponivelAsync("2030-03"); // paga

        using var inexistente = await PagarFaturaAsync(client, cartao, "2029-01");
        await ApiBank.AssertProblemaAsync(inexistente, HttpStatusCode.NotFound, Tipo("fatura-nao-encontrada"));

        async Task AssertNaoDisponivelAsync(string competencia)
        {
            using var resposta = await PagarFaturaAsync(client, cartao, competencia);
            await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, Tipo("fatura-nao-disponivel"));
        }
    }

    [Fact]
    public async Task MesmaChave_DevolveOOriginal_OutraCompetenciaDiverge_SemChave_400()
    {
        using var client = factory.CreateClient();
        var cartao = await ComFaturaDisponivelAsync(client, 2031, 100m);
        var chave = Guid.NewGuid();

        using var primeira = await PagarFaturaAsync(client, cartao, "2031-03", chave);
        using var repetida = await PagarFaturaAsync(client, cartao, "2031-03", chave);
        using var divergente = await PagarFaturaAsync(client, cartao, "2031-04", chave);
        using var semChave = await PostAsync(client, cartao.Conta.Token, $"{Faturas}/2031-03/pagar");

        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(await primeira.Content.ReadAsStringAsync(Ct), await repetida.Content.ReadAsStringAsync(Ct));
        Assert.Equal(4900m, await SaldoAsync(client, cartao.Conta));
        await ApiBank.AssertProblemaAsync(divergente, HttpStatusCode.Conflict, Tipo("idempotencia-divergente"));
        var json = await ApiBank.AssertProblemaAsync(semChave, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.True(json.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
    }

    [Theory]
    [InlineData(true, 2032)]
    [InlineData(false, 2033)]
    public async Task Simultaneos_UmDebito(bool mesmaChave, int ano)
    {
        using var client = factory.CreateClient();
        var cartao = await ComFaturaDisponivelAsync(client, ano, 100m);
        var chave = Guid.NewGuid();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PagarFaturaAsync(client, cartao, $"{ano}-03", mesmaChave ? chave : Guid.NewGuid())));

        Assert.Equal(mesmaChave ? 10 : 1, respostas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(mesmaChave ? 0 : 9, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(4900m, await SaldoAsync(client, cartao.Conta));
        Assert.Equal(5000m, await LimiteDisponivelAsync(client, cartao));
        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, cartao.Conta.CarteiraId));
        foreach (var r in respostas)
        {
            r.Dispose();
        }
    }

    [Fact]
    public async Task NaoContaNoLimiteDeTransferencia_EEhPermitidoEmDiaNaoUtil()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await ComFaturaDisponivelAsync(client, 2034, 900m);
        factory.Relogio.Agora = Brasilia(2034, 3, 11, 10); // sábado: vencida na sexta-feira 10/03
        using (var pagamento = await PagarFaturaAsync(client, cartao, "2034-03"))
        {
            Assert.Equal(HttpStatusCode.OK, pagamento.StatusCode);
        }

        using var transferencia = await TransferirAsync(client, cartao.Conta, pj.Documento, 1000m); // limite diário de R$ 1.000,00 intacto

        Assert.Equal(HttpStatusCode.Created, transferencia.StatusCode);
    }

    /// <summary>Cartão com uma compra em 01/03 e a fatura de março liberada em 08/03 (ou no próximo dia útil).</summary>
    private async Task<CartaoTeste> ComFaturaDisponivelAsync(HttpClient client, int ano, decimal valor)
    {
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        // Validade do cartão a partir do ano do teste.
        factory.Relogio.Agora = Brasilia(ano, 3, 1, 10);
        if (Bank.Domain.Cartoes.ValidadeCartao.Criar(cartao.Validade).Valor.EstaVencida(new DateOnly(ano, 3, 1)))
        {
            factory.Relogio.Agora = null;
            var conta = await CriarPfAsync(client);
            factory.Relogio.Agora = Brasilia(ano, 1, 2, 10);
            cartao = await EmitirCartaoAsync(client, conta);
            factory.Relogio.Agora = Brasilia(ano, 3, 1, 10);
        }

        await ComprarAsync(client, cartao, pj, valor);
        var liberacao = Bank.Domain.Calendario.CalendarioBancario.ProximoDiaUtil(new DateOnly(ano, 3, 8));
        await factory.ProcessarEmAsync(Brasilia(ano, 3, liberacao.Day, 0, 0, 0));
        Assert.Equal("Disponivel", (await FaturaAsync(client, cartao, $"{ano}-03")).GetProperty("situacao").GetString());
        return cartao;
    }
}
