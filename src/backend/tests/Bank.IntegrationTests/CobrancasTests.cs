using System.Net;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Emissão, consulta, lista e cancelamento (specs/014-cartao-credito-cobrancas, US2; FR-009 a FR-013).</summary>
public class CobrancasTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>
{
    [Fact]
    public async Task Emitir_201_Pendente()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        var json = await PostOkAsync(client, pj.Token, Cobrancas, new { valor = 299.90m, descricao = "  Café Especial " }, status: HttpStatusCode.Created);

        Assert.Equal(
            ["cobrancaId", "lojista", "valor", "descricao", "status", "criadoEm", "pagaEm", "canceladaEm", "pagamento", "ultimaRecusa"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("299.90", "Café Especial", "Pendente"), (json.GetProperty("valor").GetRawText(), json.GetProperty("descricao").GetString(), json.GetProperty("status").GetString()));
        var lojista = json.GetProperty("lojista");
        Assert.Equal((pj.UsuarioId, "Empresa Exemplo Ltda", pj.Documento), (lojista.GetProperty("usuarioId").GetGuid(), lojista.GetProperty("nome").GetString(), lojista.GetProperty("documento").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("pagamento").ValueKind);
    }

    public static TheoryData<object, string[]> Invalidas() => new()
    {
        { new { valor = 0m, descricao = "x" }, ["valor"] },
        { new { valor = -1m, descricao = "x" }, ["valor"] },
        { new { valor = 1.999m, descricao = "x" }, ["valor"] },
        { new { valor = 10000000000000000m, descricao = "x" }, ["valor"] },
        { new { descricao = "x" }, ["valor"] },
        { new { valor = 1m, descricao = "" }, ["descricao"] },
        { new { valor = 1m, descricao = "   " }, ["descricao"] },
        { new { valor = 1m }, ["descricao"] },
        { new { valor = 1m, descricao = new string('a', 201) }, ["descricao"] },
        { new { valor = 0m, descricao = "" }, ["valor", "descricao"] },
    };

    [Theory]
    [MemberData(nameof(Invalidas))]
    public async Task Emitir_Invalida_400PorCampo(object corpo, string[] campos)
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        using var resposta = await PostAsync(client, pj.Token, Cobrancas, corpo);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.Equal(campos.Order(StringComparer.Ordinal), json.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Consultar_PagamentoERecusaSoParaQuemPodeVer()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var pagador = await CriarPfComCartaoAsync(factory, client);
        var terceiro = await CriarPfAsync(client);
        var outraPj = await CriarPjAsync(client);
        var cobranca = await EmitirAsync(client, pj);
        (await PagarAsync(client, pagador, cobranca, corpo: pagador.Corpo(cvv: pagador.Cvv == "000" ? "001" : "000"))).Dispose();

        var recusadaPj = await GetOkAsync(client, pj.Token, $"{Cobrancas}/{cobranca}");
        Assert.Equal("CartaoNaoAutorizado", recusadaPj.GetProperty("ultimaRecusa").GetProperty("motivo").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await GetOkAsync(client, pagador.Conta.Token, $"{Cobrancas}/{cobranca}")).GetProperty("ultimaRecusa").ValueKind);

        (await PagarAsync(client, pagador, cobranca)).Dispose();

        foreach (var token in new[] { pj.Token, pagador.Conta.Token })
        {
            var pagamento = (await GetOkAsync(client, token, $"{Cobrancas}/{cobranca}")).GetProperty("pagamento");
            Assert.Equal((pagador.UltimosDigitos, "Visa"), (pagamento.GetProperty("ultimosDigitosCartao").GetString(), pagamento.GetProperty("bandeira").GetProperty("nome").GetString()));
            Assert.Matches("^[A-Z0-9]{6}$", pagamento.GetProperty("codigoAutorizacao").GetString());
        }

        foreach (var token in new[] { terceiro.Token, outraPj.Token })
        {
            var json = await GetOkAsync(client, token, $"{Cobrancas}/{cobranca}");
            Assert.Equal("Paga", json.GetProperty("status").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("pagamento").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.GetProperty("ultimaRecusa").ValueKind);
        }

        // A identidade do pagador nunca aparece, nem para o lojista.
        var corpo = (await GetOkAsync(client, pj.Token, $"{Cobrancas}/{cobranca}")).GetRawText();
        Assert.DoesNotContain(pagador.Conta.UsuarioId.ToString(), corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(pagador.Conta.Documento, corpo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc", HttpStatusCode.BadRequest, "validacao")]
    [InlineData("00000000-0000-0000-0000-000000000000", HttpStatusCode.BadRequest, "validacao")]
    [InlineData("019a8f77-1111-7890-abcd-ef1234567890", HttpStatusCode.NotFound, "cobranca-nao-encontrada")]
    public async Task ConsultarECancelar_IdInvalidoOuInexistente(string id, HttpStatusCode status, string codigo)
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        using var consulta = await GetComTokenAsync(client, pj.Token, $"{Cobrancas}/{id}");
        using var cancelamento = await PostAsync(client, pj.Token, $"{Cobrancas}/{id}/cancelar");

        await ApiBank.AssertProblemaAsync(consulta, status, Tipo(codigo));
        await ApiBank.AssertProblemaAsync(cancelamento, status, Tipo(codigo));
    }

    [Fact]
    public async Task Listar_SoAsProprias_DasMaisNovasParaAsMaisAntigas_ComFiltroEPaginas()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var outra = await CriarPjAsync(client);
        await EmitirAsync(client, outra);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await EmitirAsync(client, pj, 10m + i));
        }

        await PostOkAsync(client, pj.Token, $"{Cobrancas}/{ids[1]}/cancelar");
        ids.Reverse();

        var todas = await GetOkAsync(client, pj.Token, MinhasCobrancas);
        Assert.Equal(ids, todas.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("cobrancaId").GetGuid()));
        Assert.Equal((1, 20, 5), (todas.GetProperty("paginaAtual").GetInt32(), todas.GetProperty("tamanhoPagina").GetInt32(), todas.GetProperty("totalItens").GetInt32()));

        var pendentes = await GetOkAsync(client, pj.Token, MinhasCobrancas + "?status=Pendente&pagina=2&tamanhoPagina=3");
        Assert.Equal(4, pendentes.GetProperty("totalItens").GetInt32());
        Assert.Single(pendentes.GetProperty("itens").EnumerateArray());

        var canceladas = await GetOkAsync(client, pj.Token, MinhasCobrancas + "?status=Cancelada");
        Assert.Equal(ids[3], Assert.Single(canceladas.GetProperty("itens").EnumerateArray()).GetProperty("cobrancaId").GetGuid());
    }

    [Theory]
    [InlineData("?status=pendente", "status")]
    [InlineData("?status=1", "status")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    public async Task Listar_ParametroInvalido_400(string consulta, string campo)
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        using var resposta = await GetComTokenAsync(client, pj.Token, MinhasCobrancas + consulta);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, Tipo("validacao"));
        Assert.True(json.GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Cancelar_Pendente_FicaCanceladaENaoPodeSerPaga()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var pagador = await CriarPfComCartaoAsync(factory, client);
        var cobranca = await EmitirAsync(client, pj);

        var json = await PostOkAsync(client, pj.Token, $"{Cobrancas}/{cobranca}/cancelar");

        Assert.Equal("Cancelada", json.GetProperty("status").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.String, json.GetProperty("canceladaEm").ValueKind);
        using var pagamento = await PagarAsync(client, pagador, cobranca);
        await ApiBank.AssertProblemaAsync(pagamento, HttpStatusCode.Conflict, Tipo("cobranca-nao-pendente"));
        using var deNovo = await PostAsync(client, pj.Token, $"{Cobrancas}/{cobranca}/cancelar");
        await ApiBank.AssertProblemaAsync(deNovo, HttpStatusCode.Conflict, Tipo("cobranca-nao-pendente"));
    }

    [Fact]
    public async Task Cancelar_PagaOuDeOutraPj_Recusa()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var outra = await CriarPjAsync(client);
        var pagador = await CriarPfComCartaoAsync(factory, client);
        var paga = await ComprarAsync(client, pagador, pj, 10m);
        var pendente = await EmitirAsync(client, pj);

        using var respostaPaga = await PostAsync(client, pj.Token, $"{Cobrancas}/{paga}/cancelar");
        await ApiBank.AssertProblemaAsync(respostaPaga, HttpStatusCode.Conflict, Tipo("cobranca-nao-pendente"));
        using var alheia = await PostAsync(client, outra.Token, $"{Cobrancas}/{pendente}/cancelar");
        await ApiBank.AssertProblemaAsync(alheia, HttpStatusCode.NotFound, Tipo("cobranca-nao-encontrada"));
        Assert.Equal("Pendente", (await GetOkAsync(client, pj.Token, $"{Cobrancas}/{pendente}")).GetProperty("status").GetString());
    }
}
