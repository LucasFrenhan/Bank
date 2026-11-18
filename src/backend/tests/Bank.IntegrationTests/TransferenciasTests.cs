using System.Net;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>POST /api/v1/transferencias — US1 da spec 006 (contracts/transferencias-api.md).</summary>
public class TransferenciasTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task P2B_201ComComprovanteSaldosLancamentosEEvento()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var chave = Guid.NewGuid();

        using var resposta = await TransferirAsync(client, pf, Dados.CnpjComMascara(pj.Documento).ToLowerInvariant(), 4.50m, chave, "  Almoço ");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\"valor\":4.50", corpo, StringComparison.Ordinal);
        var json = await ApiBank.LerJsonAsync(resposta);
        Assert.Equal(chave, json.GetProperty("chaveIdempotencia").GetGuid());
        Assert.Equal(pf.UsuarioId, json.GetProperty("pagador").GetProperty("usuarioId").GetGuid());
        Assert.Equal("Lucas Silva", json.GetProperty("pagador").GetProperty("nome").GetString());
        // Spec 008, FR-002a: documento do pagador mascarado.
        Assert.Equal($"•••.{pf.Documento[3..6]}.{pf.Documento[6..9]}-••", json.GetProperty("pagador").GetProperty("documento").GetString());
        var recebedor = json.GetProperty("recebedor");
        Assert.Equal((pj.UsuarioId, "Empresa Exemplo Ltda", pj.Documento, "PessoaJuridica"),
            (recebedor.GetProperty("usuarioId").GetGuid(), recebedor.GetProperty("nome").GetString(), recebedor.GetProperty("documento").GetString(), recebedor.GetProperty("tipoConta").GetString()));
        Assert.Equal(("Almoço", "Concluida"), (json.GetProperty("descricao").GetString(), json.GetProperty("status").GetString()));
        Assert.Equal(TimeSpan.Zero, json.GetProperty("criadoEm").GetDateTimeOffset().Offset);

        Assert.Equal((5.50m, 4.50m), (await SaldoAsync(client, pf), await SaldoAsync(client, pj)));
        Assert.Equal((0m, 0m), (await DiferencaSaldoRazaoAsync(factory, pf.CarteiraId), await DiferencaSaldoRazaoAsync(factory, pj.CarteiraId)));

        var transferenciaId = json.GetProperty("transferenciaId").GetGuid();
        await using var contexto = factory.CriarContexto();
        var evento = Assert.Single(await contexto.EventosOutbox.ToListAsync(Ct), e => e.Payload.Contains(transferenciaId.ToString(), StringComparison.Ordinal));
        Assert.Equal(("transferencia.concluida", 1), (evento.Tipo, evento.Versao));
        Assert.Null(evento.PublicadoEm);
    }

    [Fact]
    public async Task P2P_ParaCpfMascarado_EAteZerarOSaldo()
    {
        using var client = factory.CreateClient();
        var pagador = await CriarPfAsync(client);
        var recebedor = await CriarPfAsync(client);

        using var resposta = await TransferirAsync(client, pagador, Dados.CpfComMascara(recebedor.Documento), 10.00m);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal((0m, 20m), (await SaldoAsync(client, pagador), await SaldoAsync(client, recebedor)));
    }

    [Fact]
    public async Task PessoaJuridica_403()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        var pf = await CriarPfAsync(client);

        using var resposta = await TransferirAsync(client, pj, pf.Documento, 1m);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Forbidden, "urn:bank:problema:acesso-negado");
    }

    [Fact]
    public async Task SemToken_401()
    {
        using var client = factory.CreateClient();

        using var resposta = await client.PostAsync(Transferencias, null, Ct);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.Unauthorized, "urn:bank:problema:nao-autenticado");
    }

    [Fact]
    public async Task Recusas_404_422_SemAlterarSaldo()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);

        using var inexistente = await TransferirAsync(client, pf, Dados.CnpjAlfanumerico(), 1m);
        using var propria = await TransferirAsync(client, pf, pf.Documento, 1m);
        using var semSaldo = await TransferirAsync(client, pf, pj.Documento, 10.01m);

        await ApiBank.AssertProblemaAsync(inexistente, HttpStatusCode.NotFound, "urn:bank:problema:destinatario-nao-encontrado");
        await ApiBank.AssertProblemaAsync(propria, HttpStatusCode.UnprocessableEntity, "urn:bank:problema:autotransferencia");
        await ApiBank.AssertProblemaAsync(semSaldo, HttpStatusCode.UnprocessableEntity, "urn:bank:problema:saldo-insuficiente");
        Assert.Equal((10m, 0m), (await SaldoAsync(client, pf), await SaldoAsync(client, pj)));

        await using var contexto = factory.CriarContexto();
        Assert.False(await contexto.Transferencias.AnyAsync(t => t.CarteiraOrigemId == pf.CarteiraId, Ct));
        Assert.DoesNotContain(await contexto.EventosOutbox.ToListAsync(Ct), e => e.Payload.Contains(pf.UsuarioId.ToString(), StringComparison.Ordinal));
    }

    public static TheoryData<string, string> CamposInvalidos() => new()
    {
        { "{\"documentoDestino\":\"DOC\",\"valor\":1}", "Idempotency-Key" },
        { "{\"documentoDestino\":\"DOC\",\"valor\":0}", "valor" },
        { "{\"documentoDestino\":\"DOC\",\"valor\":-1}", "valor" },
        { "{\"documentoDestino\":\"DOC\",\"valor\":10.005}", "valor" },
        { "{\"documentoDestino\":\"DOC\",\"valor\":1,\"descricao\":\"" + new string('a', 201) + "\"}", "descricao" },
        { "{\"documentoDestino\":\"123\",\"valor\":1}", "documentoDestino" },
    };

    [Theory]
    [MemberData(nameof(CamposInvalidos))]
    public async Task CampoInvalido_400ComErroNoCampo(string corpoJson, string campo)
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Transferencias)
        {
            Content = new StringContent(corpoJson.Replace("DOC", pj.Documento, StringComparison.Ordinal), System.Text.Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pf.Token);
        if (campo != "Idempotency-Key")
        {
            requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        using var resposta = await client.SendAsync(requisicao, Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty(campo, out _), campo);
        Assert.Equal(10m, await SaldoAsync(client, pf));
    }

    [Fact]
    public async Task ChaveNaoUuid_400()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Transferencias) { Content = System.Net.Http.Json.JsonContent.Create(new { documentoDestino = pj.Documento, valor = 1 }) };
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pf.Token);
        requisicao.Headers.Add("Idempotency-Key", "abc");

        using var resposta = await client.SendAsync(requisicao, Ct);

        var json = await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.BadRequest, "urn:bank:problema:validacao");
        Assert.True(json.GetProperty("errors").TryGetProperty("Idempotency-Key", out _));
    }
}
