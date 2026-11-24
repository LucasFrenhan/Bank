using System.Net;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Consulta de faturas (specs/014-cartao-credito-cobrancas, US4; FR-025, FR-027).</summary>
public class FaturasTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>, IDisposable
{
    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CicloAtualSemCompras_ExisteComTotalZero()
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);
        factory.Relogio.Agora = Brasilia(2027, 11, 8, 0, 0, 0);

        var fatura = await FaturaAsync(client, cartao, "2027-12");

        Assert.Equal(
            ["competencia", "inicio", "fechamento", "vencimento", "situacao", "total", "diasAtraso", "juros", "valorAPagar", "pagaEm", "valorPago", "lancamentos"],
            fatura.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("2027-12", "2027-11-08", "2027-12-07", "2027-12-10", "Aberta", "0.00", 0),
            (fatura.GetProperty("competencia").GetString(), fatura.GetProperty("inicio").GetString(), fatura.GetProperty("fechamento").GetString(),
             fatura.GetProperty("vencimento").GetString(), fatura.GetProperty("situacao").GetString(), fatura.GetProperty("total").GetRawText(), fatura.GetProperty("lancamentos").GetArrayLength()));

        var lista = await GetOkAsync(client, cartao.Conta.Token, Faturas);
        Assert.Equal("2027-12", Assert.Single(lista.GetProperty("itens").EnumerateArray()).GetProperty("competencia").GetString());
        await using var contexto = factory.CriarContexto();
        Assert.Empty(contexto.Faturas.Where(f => contexto.Cartoes.Any(c => c.Id == f.CartaoId && c.UsuarioId == Bank.Domain.Usuarios.UsuarioId.De(cartao.Conta.UsuarioId))));
    }

    [Fact]
    public async Task Lista_UmaPorMes_DaMaisNovaParaAMaisAntiga_ComPaginas_SemMesesVazios()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        foreach (var mes in new[] { 1, 2, 4 }) // março fica sem compras
        {
            factory.Relogio.Agora = Brasilia(2028, mes, 3, 10);
            await ComprarAsync(client, cartao, pj, 10m + mes);
            await factory.ProcessarEmAsync(Brasilia(2028, mes, 10, 0, 0, 0));
            factory.Relogio.Agora = Brasilia(2028, mes, 10, 9);
            using var pagamento = await PagarFaturaAsync(client, cartao, $"2028-{mes:D2}");
            Assert.Equal(HttpStatusCode.OK, pagamento.StatusCode);
        }

        factory.Relogio.Agora = Brasilia(2028, 5, 15, 9);

        var lista = await GetOkAsync(client, cartao.Conta.Token, Faturas);
        Assert.Equal([("2028-06", "Aberta"), ("2028-04", "Paga"), ("2028-02", "Paga"), ("2028-01", "Paga")],
            lista.GetProperty("itens").EnumerateArray().Select(f => (f.GetProperty("competencia").GetString(), f.GetProperty("situacao").GetString())));
        Assert.Equal(4, lista.GetProperty("totalItens").GetInt32());
        Assert.False(lista.GetProperty("itens")[0].TryGetProperty("lancamentos", out _));

        var pagina = await GetOkAsync(client, cartao.Conta.Token, Faturas + "?pagina=2&tamanhoPagina=3");
        Assert.Equal("2028-01", Assert.Single(pagina.GetProperty("itens").EnumerateArray()).GetProperty("competencia").GetString());

        var detalhe = await FaturaAsync(client, cartao, "2028-02");
        Assert.Equal((12m, 12m), (detalhe.GetProperty("total").GetDecimal(), detalhe.GetProperty("valorPago").GetDecimal()));
        Assert.Single(detalhe.GetProperty("lancamentos").EnumerateArray());

        using var vazio = await GetComTokenAsync(client, cartao.Conta.Token, $"{Faturas}/2028-03");
        await ApiBank.AssertProblemaAsync(vazio, HttpStatusCode.NotFound, Tipo("fatura-nao-encontrada"));
    }

    [Fact]
    public async Task Lancamentos_DoMaisNovoParaOMaisAntigo()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartao = await CriarPfComCartaoAsync(factory, client);
        foreach (var dia in new[] { 12, 14, 13 })
        {
            factory.Relogio.Agora = Brasilia(2028, 9, dia, 10);
            await ComprarAsync(client, cartao, pj, dia);
        }

        await factory.ProcessarEmAsync(Brasilia(2028, 9, 18, 9));

        var fatura = await FaturaAsync(client, cartao, "2028-10");
        Assert.Equal(["2028-09-14", "2028-09-13", "2028-09-12"], fatura.GetProperty("lancamentos").EnumerateArray().Select(l => l.GetProperty("dataCompra").GetString()));
    }

    [Theory]
    [InlineData("2028")]
    [InlineData("2028-13")]
    [InlineData("12-2028")]
    public async Task CompetenciaInvalida_400(string competencia)
    {
        using var client = factory.CreateClient();
        var cartao = await CriarPfComCartaoAsync(factory, client);

        using var consulta = await GetComTokenAsync(client, cartao.Conta.Token, $"{Faturas}/{competencia}");
        using var pagamento = await PagarFaturaAsync(client, cartao, competencia);

        foreach (var resposta in new[] { consulta, pagamento })
        {
            var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
            Assert.True(json.GetProperty("errors").TryGetProperty("competencia", out _));
        }
    }

    [Fact]
    public async Task SemCartao_404_EPaginacaoInvalida_400()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var lista = await GetComTokenAsync(client, pf.Token, Faturas);
        using var detalhe = await GetComTokenAsync(client, pf.Token, $"{Faturas}/2028-01");
        using var pagamento = await PostAsync(client, pf.Token, $"{Faturas}/2028-01/pagar", chave: Guid.NewGuid());
        using var invalida = await GetComTokenAsync(client, pf.Token, Faturas + "?pagina=x");

        await ApiBank.AssertProblemaAsync(lista, HttpStatusCode.NotFound, Tipo("cartao-nao-encontrado"));
        await ApiBank.AssertProblemaAsync(detalhe, HttpStatusCode.NotFound, Tipo("cartao-nao-encontrado"));
        await ApiBank.AssertProblemaAsync(pagamento, HttpStatusCode.NotFound, Tipo("cartao-nao-encontrado"));
        await ApiBank.AssertProblemaAsync(invalida, HttpStatusCode.BadRequest, Tipo("validacao"));
    }

    [Fact]
    public async Task FaturaDeOutraPessoa_NaoAparece()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var (dono, outro) = (await CriarPfComCartaoAsync(factory, client), await CriarPfComCartaoAsync(factory, client));
        factory.Relogio.Agora = Brasilia(2029, 7, 3, 10);
        await ComprarAsync(client, dono, pj, 10m);
        factory.Relogio.Agora = Brasilia(2029, 9, 3, 10);

        using var resposta = await GetComTokenAsync(client, outro.Conta.Token, $"{Faturas}/2029-07");

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, Tipo("fatura-nao-encontrada"));
    }
}
