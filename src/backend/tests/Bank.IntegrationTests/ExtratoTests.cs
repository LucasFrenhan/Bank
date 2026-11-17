using System.Net;
using System.Text.Json;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>GET /api/v1/transferencias/extrato — US4 da spec 006 (FR-012 a FR-014).</summary>
public class ExtratoTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private const string Caminho = "/api/v1/transferencias/extrato";

    [Fact]
    public async Task PfNova_SoAAberturaDe10()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        var json = await ExtratoAsync(client, pf);

        var item = Assert.Single(json.GetProperty("itens").EnumerateArray());
        Assert.Equal(("Abertura", "Entrada", 10m), (item.GetProperty("tipo").GetString(), item.GetProperty("direcao").GetString(), item.GetProperty("valor").GetDecimal()));
        Assert.Equal(JsonValueKind.Null, item.GetProperty("contraparte").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("transferenciaId").ValueKind);
        Assert.Equal((1, 20, 1), (json.GetProperty("paginaAtual").GetInt32(), json.GetProperty("tamanhoPagina").GetInt32(), json.GetProperty("totalItens").GetInt32()));
    }

    [Fact]
    public async Task PagadorVeSaidaERecebedorEntrada_ComContraparte()
    {
        using var client = factory.CreateClient();
        var pagador = await CriarPfAsync(client);
        var recebedor = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        (await TransferirAsync(client, pagador, recebedor.Documento, 2.5m, descricao: "Pizza")).Dispose();
        (await TransferirAsync(client, pagador, pj.Documento, 1m)).Dispose();

        var dePagador = (await ExtratoAsync(client, pagador)).GetProperty("itens").EnumerateArray().ToList();
        var deRecebedor = (await ExtratoAsync(client, recebedor)).GetProperty("itens").EnumerateArray().ToList();
        var dePj = (await ExtratoAsync(client, pj)).GetProperty("itens").EnumerateArray().ToList();

        // Mais recente primeiro: a transferência para a PJ, depois a para a PF, depois a abertura.
        Assert.Equal(["Saida", "Saida", "Entrada"], dePagador.Select(i => i.GetProperty("direcao").GetString()));
        var paraPj = dePagador[0].GetProperty("contraparte");
        Assert.Equal((pj.Documento, "PessoaJuridica"), (paraPj.GetProperty("documento").GetString(), paraPj.GetProperty("tipoConta").GetString()));
        var paraPf = dePagador[1].GetProperty("contraparte");
        Assert.Equal("Lucas Silva", paraPf.GetProperty("nome").GetString());
        Assert.Equal($"•••.{recebedor.Documento[3..6]}.{recebedor.Documento[6..9]}-••", paraPf.GetProperty("documento").GetString());
        Assert.Equal(("Pizza", 2.5m), (dePagador[1].GetProperty("descricao").GetString(), dePagador[1].GetProperty("valor").GetDecimal()));

        var entrada = deRecebedor[0];
        Assert.Equal(("Transferencia", "Entrada"), (entrada.GetProperty("tipo").GetString(), entrada.GetProperty("direcao").GetString()));
        Assert.Equal(dePagador[1].GetProperty("transferenciaId").GetGuid(), entrada.GetProperty("transferenciaId").GetGuid());
        Assert.Equal($"•••.{pagador.Documento[3..6]}.{pagador.Documento[6..9]}-••", entrada.GetProperty("contraparte").GetProperty("documento").GetString());

        var soma = dePagador.Sum(i => (i.GetProperty("direcao").GetString() == "Entrada" ? 1 : -1) * i.GetProperty("valor").GetDecimal());
        Assert.Equal(await SaldoAsync(client, pagador), soma);
        Assert.Single(dePj);
    }

    [Fact]
    public async Task PaginacaoEFiltroDeDirecao()
    {
        using var client = factory.CreateClient();
        var conta = await CriarPfAsync(client);
        var origem = await CriarPfAsync(client);
        var destino = await CriarPjAsync(client);
        for (var i = 0; i < 25; i++)
        {
            (await TransferirAsync(client, origem, conta.Documento, 0.10m)).Dispose();
        }

        for (var i = 0; i < 5; i++)
        {
            (await TransferirAsync(client, conta, destino.Documento, 0.50m)).Dispose();
        }

        // 25 entradas + 5 saídas + a abertura = 31.
        var pagina1 = await ExtratoAsync(client, conta);
        var pagina2 = await ExtratoAsync(client, conta, "?pagina=2");
        var alem = await ExtratoAsync(client, conta, "?pagina=5");
        var saidas = await ExtratoAsync(client, conta, "?direcao=Saida");
        var entradas = await ExtratoAsync(client, conta, "?direcao=Entrada&tamanhoPagina=100");

        Assert.Equal((20, 31), (pagina1.GetProperty("itens").GetArrayLength(), pagina1.GetProperty("totalItens").GetInt32()));
        Assert.Equal((11, 31), (pagina2.GetProperty("itens").GetArrayLength(), pagina2.GetProperty("totalItens").GetInt32()));
        Assert.Equal((0, 31), (alem.GetProperty("itens").GetArrayLength(), alem.GetProperty("totalItens").GetInt32()));
        Assert.Equal(5, saidas.GetProperty("totalItens").GetInt32());
        Assert.All(saidas.GetProperty("itens").EnumerateArray(), i => Assert.Equal("Saida", i.GetProperty("direcao").GetString()));
        Assert.Equal((26, 26), (entradas.GetProperty("totalItens").GetInt32(), entradas.GetProperty("itens").GetArrayLength()));

        var datas = pagina1.GetProperty("itens").EnumerateArray().Concat(pagina2.GetProperty("itens").EnumerateArray())
            .Select(i => i.GetProperty("criadoEm").GetDateTimeOffset()).ToList();
        Assert.Equal(datas.OrderDescending(), datas);
    }

    [Theory]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    [InlineData("?direcao=Todas", "direcao")]
    public async Task ParametroInvalido_400(string query, string campo)
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);

        using var resposta = await GetComTokenAsync(client, pf.Token, Caminho + query);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty(campo, out _), campo);
    }

    [Fact]
    public async Task PjSemMovimentacoes_Vazio_SemToken_401()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);

        var json = await ExtratoAsync(client, pj);
        using var semToken = await client.GetAsync(new Uri(Caminho, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (json.GetProperty("itens").GetArrayLength(), json.GetProperty("totalItens").GetInt32()));
        await ApiBank.AssertProblemaAsync(semToken, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }

    private static async Task<JsonElement> ExtratoAsync(HttpClient client, ContaTeste conta, string query = "")
    {
        using var resposta = await GetComTokenAsync(client, conta.Token, Caminho + query);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await ApiBank.LerJsonAsync(resposta);
    }
}
