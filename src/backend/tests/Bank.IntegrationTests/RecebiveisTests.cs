using System.Net;
using Bank.Domain.Calendario;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Recebíveis do lojista (specs/014-cartao-credito-cobrancas, US6; FR-030 a FR-034; SC-008).</summary>
public class RecebiveisTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>, IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Compra_GeraRecebivelIntegralNoTrigesimoDiaUtil_ELiquidaNaData()
    {
        using var client = factory.CreateClient();
        var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2026, 11, 24, 10));

        Assert.Equal(
            ["recebivelId", "cobrancaId", "descricao", "situacao", "valorBruto", "taxa", "valorLiquido", "dataPrevista", "recebidoEm", "criadoEm"],
            recebivel.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Pendente", "299.90", "0.00", "299.90", "2027-01-07"),
            (recebivel.GetProperty("situacao").GetString(), recebivel.GetProperty("valorBruto").GetRawText(), recebivel.GetProperty("taxa").GetRawText(),
             recebivel.GetProperty("valorLiquido").GetRawText(), recebivel.GetProperty("dataPrevista").GetString()));

        await factory.ProcessarEmAsync(Brasilia(2027, 1, 6, 23));
        Assert.Equal(0m, await SaldoAsync(client, pj));

        await factory.ProcessarEmAsync(Brasilia(2027, 1, 7, 0, 0, 0));
        await factory.ProcessarAsync();

        Assert.Equal(299.90m, await SaldoAsync(client, pj));
        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pj.CarteiraId));
        var liquidado = await ObterAsync(client, pj, recebivel);
        Assert.Equal(("Liquidado", "299.90"), (liquidado.GetProperty("situacao").GetString(), liquidado.GetProperty("valorLiquido").GetRawText()));
        Assert.Equal(System.Text.Json.JsonValueKind.String, liquidado.GetProperty("recebidoEm").ValueKind);
        var item = Assert.Single((await GetOkAsync(client, pj.Token, "/api/v1/transferencias/extrato")).GetProperty("itens").EnumerateArray());
        Assert.Equal(("LiquidacaoRecebivel", "Entrada", 299.90m, "Recebimento: Café Especial"),
            (item.GetProperty("tipo").GetString(), item.GetProperty("direcao").GetString(), item.GetProperty("valor").GetDecimal(), item.GetProperty("descricao").GetString()));

        using var antecipar = await AnteciparAsync(client, pj, recebivel.GetProperty("recebivelId").GetGuid());
        await ApiBank.AssertProblemaAsync(antecipar, HttpStatusCode.Conflict, Tipo("recebivel-nao-antecipavel"));
    }

    [Fact]
    public async Task Antecipar_CreditaOLiquidoNaHora_ENaDataPrevistaNadaMais()
    {
        using var client = factory.CreateClient();
        var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2027, 3, 1, 10));
        factory.Relogio.Agora = Brasilia(2027, 3, 6, 10); // sábado: permitido

        using (var resposta = await AnteciparAsync(client, pj, Id(recebivel)))
        {
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var json = await ApiBank.LerJsonAsync(resposta);
            Assert.Equal(("Antecipado", "299.90", "6.00", "293.90"),
                (json.GetProperty("situacao").GetString(), json.GetProperty("valorBruto").GetRawText(), json.GetProperty("taxa").GetRawText(), json.GetProperty("valorLiquido").GetRawText()));
        }

        Assert.Equal(293.90m, await SaldoAsync(client, pj));
        var item = Assert.Single((await GetOkAsync(client, pj.Token, "/api/v1/transferencias/extrato")).GetProperty("itens").EnumerateArray());
        Assert.Equal(("AntecipacaoRecebivel", "Antecipação: Café Especial"), (item.GetProperty("tipo").GetString(), item.GetProperty("descricao").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("contraparte").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("transferenciaId").ValueKind);
        Assert.Equal(1, (await GetOkAsync(client, pj.Token, "/api/v1/transferencias/extrato?direcao=Entrada")).GetProperty("totalItens").GetInt32());
        Assert.Equal(0, (await GetOkAsync(client, pj.Token, "/api/v1/transferencias/extrato?direcao=Saida")).GetProperty("totalItens").GetInt32());

        await factory.ProcessarEmAsync(Brasilia(2027, 12, 1, 9));

        Assert.Equal(293.90m, await SaldoAsync(client, pj));
        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pj.CarteiraId));
        using var deNovo = await AnteciparAsync(client, pj, Id(recebivel));
        await ApiBank.AssertProblemaAsync(deNovo, HttpStatusCode.Conflict, Tipo("recebivel-nao-antecipavel"));
    }

    [Fact]
    public async Task Antecipar_NoDiaPrevisto_409_MesmoSemLiquidacao()
    {
        using var client = factory.CreateClient();
        var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2028, 3, 1, 10));
        var prevista = DateOnly.Parse(recebivel.GetProperty("dataPrevista").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(CalendarioBancario.SomarDiasUteis(new DateOnly(2028, 3, 1), 30), prevista);
        factory.Relogio.Agora = Brasilia(prevista.Year, prevista.Month, prevista.Day, 0, 0, 0);

        using var resposta = await AnteciparAsync(client, pj, Id(recebivel));

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Conflict, Tipo("recebivel-nao-antecipavel"));
        Assert.Equal(0m, await SaldoAsync(client, pj));
    }

    [Fact]
    public async Task ProcessamentoParadoNaData_CreditaDepois_UmaVez()
    {
        using var client = factory.CreateClient();
        var (pj, _) = await ComRecebivelAsync(client, Brasilia(2028, 6, 1, 10));

        factory.Relogio.Agora = Brasilia(2028, 9, 1, 9); // bem depois do 30º dia útil
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => factory.ProcessarAsync()));

        Assert.Equal(299.90m, await SaldoAsync(client, pj));
        Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pj.CarteiraId));
    }

    [Fact]
    public async Task AnteciparELiquidarAoMesmoTempo_UmCredito()
    {
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2029, 1, 2 + i, 10));
            factory.Relogio.Agora = Brasilia(2029, 1, 8 + i, 10);
            // A liquidação simulada usa um instante já na data prevista; a antecipação, o relógio atual.
            await using var contexto = factory.CriarContexto();
            var id = Bank.Domain.Recebiveis.RecebivelId.De(Id(recebivel));
            await contexto.Database.ExecuteSqlRawAsync("UPDATE recebiveis SET data_prevista = '2029-06-01' WHERE id = {0}", [id.Valor], Ct);
            factory.Relogio.Agora = Brasilia(2029, 5, 31, 23, 59, 59);
            var antecipacao = AnteciparAsync(client, pj, Id(recebivel));
            factory.Relogio.Agora = Brasilia(2029, 6, 1, 0, 0, 0);
            var processamento = factory.ProcessarAsync();
            using var resposta = await antecipacao;
            await processamento;

            var saldo = await SaldoAsync(client, pj);
            Assert.Equal(resposta.StatusCode == HttpStatusCode.OK ? 293.90m : 299.90m, saldo);
            Assert.Equal(0m, await DiferencaSaldoRazaoAsync(factory, pj.CarteiraId));
        }
    }

    [Fact]
    public async Task MesmaChave_UmCredito_EOutroRecebivelDiverge()
    {
        using var client = factory.CreateClient();
        var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2030, 1, 10, 10));
        var cartao = await CriarPfComCartaoAsync(factory, client);
        var outraCobranca = await ComprarAsync(client, cartao, pj, 10m);
        var outro = await RecebivelDaCobrancaAsync(client, pj, outraCobranca);
        var chave = Guid.NewGuid();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => AnteciparAsync(client, pj, Id(recebivel), chave)));
        using var divergente = await AnteciparAsync(client, pj, Id(outro), chave);
        using var semChave = await PostAsync(client, pj.Token, $"{Recebiveis}/{Id(outro)}/antecipar");

        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single((await Task.WhenAll(respostas.Select(r => r.Content.ReadAsStringAsync(Ct)))).Distinct());
        Assert.Equal(293.90m, await SaldoAsync(client, pj));
        await ApiBank.AssertProblemaAsync(divergente, HttpStatusCode.Conflict, Tipo("idempotencia-divergente"));
        await ApiBank.AssertProblemaAsync(semChave, HttpStatusCode.BadRequest, Tipo("validacao"));
        foreach (var r in respostas)
        {
            r.Dispose();
        }
    }

    [Fact]
    public async Task ChavesDiferentesSimultaneas_UmCredito()
    {
        using var client = factory.CreateClient();
        var (pj, recebivel) = await ComRecebivelAsync(client, Brasilia(2030, 5, 10, 10));

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => AnteciparAsync(client, pj, Id(recebivel))));

        Assert.Equal((1, 9), (respostas.Count(r => r.StatusCode == HttpStatusCode.OK), respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict)));
        Assert.Equal(293.90m, await SaldoAsync(client, pj));
        foreach (var r in respostas)
        {
            r.Dispose();
        }
    }

    [Fact]
    public async Task DeOutraPj_OuInexistente_404_EIdInvalido_400()
    {
        using var client = factory.CreateClient();
        var (_, recebivel) = await ComRecebivelAsync(client, Brasilia(2027, 9, 1, 10));
        var outra = await CriarLojistaAsync(factory, client);

        foreach (var id in new[] { Id(recebivel), Guid.NewGuid() })
        {
            using var consulta = await GetComTokenAsync(client, outra.Token, $"{Recebiveis}/{id}");
            using var antecipar = await AnteciparAsync(client, outra, id);
            await ApiBank.AssertProblemaAsync(consulta, HttpStatusCode.NotFound, Tipo("recebivel-nao-encontrado"));
            await ApiBank.AssertProblemaAsync(antecipar, HttpStatusCode.NotFound, Tipo("recebivel-nao-encontrado"));
        }

        using var invalido = await GetComTokenAsync(client, outra.Token, $"{Recebiveis}/abc");
        await ApiBank.AssertProblemaAsync(invalido, HttpStatusCode.BadRequest, Tipo("validacao"));
        using var anteciparInvalido = await PostAsync(client, outra.Token, $"{Recebiveis}/abc/antecipar", chave: Guid.NewGuid());
        await ApiBank.AssertProblemaAsync(anteciparInvalido, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.Equal(0, (await GetOkAsync(client, outra.Token, Recebiveis)).GetProperty("totalItens").GetInt32());
    }

    [Fact]
    public async Task Listar_FiltroEPaginas()
    {
        using var client = factory.CreateClient();
        var (pj, primeiro) = await ComRecebivelAsync(client, Brasilia(2028, 11, 6, 10));
        var cartao = await CriarPfComCartaoAsync(factory, client);
        for (var i = 1; i <= 3; i++)
        {
            factory.Relogio.Agora = Brasilia(2028, 11, 6, 10 + i);
            await ComprarAsync(client, cartao, pj, i);
        }

        (await AnteciparAsync(client, pj, Id(primeiro))).Dispose();

        var todos = await GetOkAsync(client, pj.Token, Recebiveis);
        Assert.Equal([3m, 2m, 1m, 299.90m], todos.GetProperty("itens").EnumerateArray().Select(r => r.GetProperty("valorBruto").GetDecimal()));
        var pendentes = await GetOkAsync(client, pj.Token, Recebiveis + "?situacao=Pendente&tamanhoPagina=2&pagina=2");
        Assert.Equal((3, 1m), (pendentes.GetProperty("totalItens").GetInt32(), Assert.Single(pendentes.GetProperty("itens").EnumerateArray()).GetProperty("valorBruto").GetDecimal()));
        Assert.Equal(1, (await GetOkAsync(client, pj.Token, Recebiveis + "?situacao=Antecipado")).GetProperty("totalItens").GetInt32());

        using var invalida = await GetComTokenAsync(client, pj.Token, Recebiveis + "?situacao=x&pagina=0");
        var json = await ApiBank.AssertProblemaAsync(invalida, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.Equal(["pagina", "situacao"], json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    private async Task<(ContaTeste Lojista, System.Text.Json.JsonElement Recebivel)> ComRecebivelAsync(HttpClient client, DateTimeOffset compra)
    {
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        factory.Relogio.Agora = compra;
        var cobranca = await ComprarAsync(client, cartao, pj);
        return (pj, await RecebivelDaCobrancaAsync(client, pj, cobranca));
    }

    private static Guid Id(System.Text.Json.JsonElement recebivel) => recebivel.GetProperty("recebivelId").GetGuid();

    private static Task<System.Text.Json.JsonElement> ObterAsync(HttpClient client, ContaTeste pj, System.Text.Json.JsonElement recebivel) =>
        GetOkAsync(client, pj.Token, $"{Recebiveis}/{Id(recebivel)}");
}
