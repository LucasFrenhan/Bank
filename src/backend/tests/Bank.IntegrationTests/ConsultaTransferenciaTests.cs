using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// GET /api/v1/transferencias/{transferenciaId} (specs/008-consulta-transferencia: US1–US3;
/// 401/403 exigidos pelo Princípio VII).
/// </summary>
public class ConsultaTransferenciaTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Caminho(object id) => $"/api/v1/transferencias/{id}";

    private static string CpfMascarado(string cpf) => $"•••.{cpf[3..6]}.{cpf[6..9]}-••";

    private static async Task<(ContaTeste Pagador, ContaTeste Recebedor, JsonElement Comprovante)> NovaTransferenciaAsync(HttpClient client, bool recebedorPj = true)
    {
        var pagador = await CriarPfAsync(client);
        var recebedor = recebedorPj ? await CriarPjAsync(client) : await CriarPfAsync(client);
        using var resposta = await TransferirAsync(client, pagador, recebedor.Documento, 4.50m, Guid.NewGuid(), "Café");
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (pagador, recebedor, await ApiBank.LerJsonAsync(resposta));
    }

    // ---------- US1: pagador ----------

    [Fact]
    public async Task Pagador_NovaSessao_200IgualAoComprovanteDoEnvio_SemAlterarNada()
    {
        using var client = factory.CreateClient();
        var (pagador, recebedor, comprovante) = await NovaTransferenciaAsync(client);
        var id = comprovante.GetProperty("transferenciaId").GetGuid();
        var novoToken = await ApiBank.ObterTokenAsync(client, pagador.Email);

        await using var antes = factory.CriarContexto();
        var (lancamentosAntes, eventosAntes) = (await antes.Lancamentos.CountAsync(Ct), await antes.EventosOutbox.CountAsync(Ct));
        var saldosAntes = (await SaldoAsync(client, pagador), await SaldoAsync(client, recebedor));

        using var primeira = await GetComTokenAsync(client, novoToken, Caminho(id));
        using var segunda = await GetComTokenAsync(client, novoToken, Caminho(id));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (primeira.StatusCode, segunda.StatusCode));
        var corpo = await ApiBank.LerJsonAsync(primeira);
        Assert.Equal(comprovante.GetRawText(), corpo.GetRawText());
        Assert.Equal(corpo.GetRawText(), (await ApiBank.LerJsonAsync(segunda)).GetRawText());
        Assert.Equal(CpfMascarado(pagador.Documento), corpo.GetProperty("pagador").GetProperty("documento").GetString());

        // SC-006: somente leitura.
        await using var depois = factory.CriarContexto();
        Assert.Equal((lancamentosAntes, eventosAntes), (await depois.Lancamentos.CountAsync(Ct), await depois.EventosOutbox.CountAsync(Ct)));
        Assert.Equal(saldosAntes, (await SaldoAsync(client, pagador), await SaldoAsync(client, recebedor)));
    }

    [Fact]
    public async Task Pagador_20Consultas_PeloMenos19AbaixoDe1Segundo()
    {
        using var client = factory.CreateClient();
        var (pagador, _, comprovante) = await NovaTransferenciaAsync(client);
        var caminho = Caminho(comprovante.GetProperty("transferenciaId").GetGuid());

        var rapidas = 0;
        for (var i = 0; i < 20; i++)
        {
            var cronometro = Stopwatch.StartNew();
            using var resposta = await GetComTokenAsync(client, pagador.Token, caminho);
            cronometro.Stop();
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            if (cronometro.Elapsed < TimeSpan.FromSeconds(1))
            {
                rapidas++;
            }
        }

        Assert.True(rapidas >= 19, $"SC-004: {rapidas}/20 consultas abaixo de 1 s");
    }

    // ---------- US2: recebedor ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recebedor_200SemAChaveDoPagador(bool recebedorPj)
    {
        using var client = factory.CreateClient();
        var (pagador, recebedor, comprovante) = await NovaTransferenciaAsync(client, recebedorPj);
        var chave = comprovante.GetProperty("chaveIdempotencia").GetGuid();

        using var resposta = await GetComTokenAsync(client, recebedor.Token, Caminho(comprovante.GetProperty("transferenciaId").GetGuid()));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(chave.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        var corpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("chaveIdempotencia").ValueKind);
        Assert.Equal(CpfMascarado(pagador.Documento), corpo.GetProperty("pagador").GetProperty("documento").GetString());
        Assert.Equal(recebedor.Documento, corpo.GetProperty("recebedor").GetProperty("documento").GetString());
        foreach (var campo in new[] { "transferenciaId", "pagador", "recebedor", "valor", "descricao", "status", "criadoEm" })
        {
            Assert.Equal(comprovante.GetProperty(campo).GetRawText(), corpo.GetProperty(campo).GetRawText());
        }
    }

    // ---------- US3: terceiros, inexistentes, inválidos, 401 e 403 ----------

    [Fact]
    public async Task TerceiroEInexistente_404Identicos()
    {
        using var client = factory.CreateClient();
        var (_, _, comprovante) = await NovaTransferenciaAsync(client);
        var terceiroPf = await CriarPfAsync(client);
        var terceiroPj = await CriarPjAsync(client);
        var caminho = Caminho(comprovante.GetProperty("transferenciaId").GetGuid());

        using var dePf = await GetComTokenAsync(client, terceiroPf.Token, caminho);
        using var dePj = await GetComTokenAsync(client, terceiroPj.Token, caminho);
        using var inexistente = await GetComTokenAsync(client, terceiroPf.Token, Caminho(Guid.CreateVersion7()));

        var tipo = "urn:bank:problema:transferencia-nao-encontrada";
        var problemas = new[]
        {
            await ApiBank.AssertProblemaAsync(dePf, HttpStatusCode.NotFound, tipo),
            await ApiBank.AssertProblemaAsync(dePj, HttpStatusCode.NotFound, tipo),
            await ApiBank.AssertProblemaAsync(inexistente, HttpStatusCode.NotFound, tipo),
        };

        static string SemTrace(JsonElement problema) =>
            string.Join('|', problema.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value.GetRawText()}"));
        Assert.Single(problemas.Select(SemTrace).Distinct());
    }

    [Fact]
    public async Task IdentificadorInvalido_400ComErroNoCampo()
    {
        using var client = factory.CreateClient();
        var conta = await CriarPfAsync(client);

        using var resposta = await GetComTokenAsync(client, conta.Token, Caminho("abc"));

        var problema = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(problema.GetProperty("errors").TryGetProperty("transferenciaId", out _));
    }

    [Fact]
    public async Task RotaDoExtrato_ContinuaRespondendoOExtrato()
    {
        using var client = factory.CreateClient();
        var conta = await CriarPfAsync(client);

        using var resposta = await GetComTokenAsync(client, conta.Token, "/api/v1/transferencias/extrato");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True((await ApiBank.LerJsonAsync(resposta)).TryGetProperty("itens", out _));
    }

    [Theory]
    [InlineData("sem-token")]
    [InlineData("adulterado")]
    public async Task SemTokenOuTokenInvalido_401(string cenario)
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        var caminho = Caminho(Guid.CreateVersion7());

        using var resposta = cenario == "sem-token"
            ? await client.GetAsync(new Uri(caminho, UriKind.Relative), Ct)
            : await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims)[..^4] + "AAAA", caminho);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }

    [Fact]
    public async Task TokenSemClaimsExigidas_403()
    {
        using var client = factory.CreateClient();
        var claims = TokensDeTeste.Claims(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString());
        claims.Remove("sub");

        using var resposta = await GetComTokenAsync(client, TokensDeTeste.Criar(factory.ChaveJwt, claims), Caminho(Guid.CreateVersion7()));

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }
}
