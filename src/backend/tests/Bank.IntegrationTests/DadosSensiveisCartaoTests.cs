using System.Net;
using Bank.Application.Processamento;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Logs;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Número completo e CVV nunca aparecem em banco, logs, spans, métricas, outbox ou erros, e as métricas têm as tags
/// do plano (specs/014-cartao-credito-cobrancas, FR-008, FR-037; SC-009; research R2).
/// </summary>
public class DadosSensiveisCartaoTests(TelemetriaCompletaApiFactory factory) : IClassFixture<TelemetriaCompletaApiFactory>
{
    /// <summary>Colunas de código de 3 dígitos, que podem coincidir com um CVV.</summary>
    private static readonly string[] CodigosDeTresDigitos = ["codigo", "codigo_banco", "codigo_bandeira", "moeda"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NadaVaza_EMetricasSaemComAsTagsDoPlano()
    {
        using var client = factory.CreateClient();
        factory.IniciarCapturaDeMetricas();
        var pj = await CriarPjAsync(client);
        var cartao = await EmitirCartaoAsync(client, await CriarPfAsync(client));
        var corposDeErro = new List<string>();

        var paga = await EmitirAsync(client, pj, 10m);
        var recusada = await EmitirAsync(client, pj, 5000.01m);
        var cancelada = await EmitirAsync(client, pj, 1m);
        await PostOkAsync(client, pj.Token, $"{Cobrancas}/{cancelada}/cancelar");
        (await PagarAsync(client, cartao, paga)).Dispose();
        await GuardarErroAsync(PagarAsync(client, cartao, recusada), HttpStatusCode.UnprocessableEntity);
        await GuardarErroAsync(PagarAsync(client, cartao, paga), HttpStatusCode.Conflict);
        await GuardarErroAsync(PagarAsync(client, cartao, recusada, corpo: cartao.Corpo(validade: "13/99")), HttpStatusCode.BadRequest);
        await GuardarErroAsync(PagarAsync(client, cartao, recusada, corpo: cartao.Corpo(validade: "12/99")), HttpStatusCode.UnprocessableEntity);
        await GuardarErroAsync(PagarAsync(client, cartao, recusada, corpo: new { numeroCartao = cartao.Numero, cvv = int.Parse(cartao.Cvv, System.Globalization.CultureInfo.InvariantCulture) }), HttpStatusCode.BadRequest);
        (await AnteciparAsync(client, pj, (await RecebivelDaCobrancaAsync(client, pj, paga)).GetProperty("recebivelId").GetGuid())).Dispose();

        // Respostas de erro, logs e spans.
        Assert.All(corposDeErro, c => Assert.DoesNotContain(cartao.Numero, c, StringComparison.Ordinal));
        var logs = factory.Logs.Copiar();
        Assert.NotEmpty(logs);
        Assert.All(logs, l =>
        {
            Assert.DoesNotContain(cartao.Numero, Texto(l), StringComparison.Ordinal);
            if (l.CategoryName?.StartsWith("Bank.", StringComparison.Ordinal) == true)
            {
                Assert.DoesNotContain(l.Attributes ?? [], a => Equals(a.Value?.ToString(), cartao.Cvv));
            }
        });
        Assert.All(factory.Spans.Copiar(), s => Assert.DoesNotContain(s.TagObjects, t =>
            t.Value?.ToString()?.Contains(cartao.Numero, StringComparison.Ordinal) == true));
        Assert.DoesNotContain(factory.Medicoes, m => m.Tags.Values.Any(v => v == cartao.Numero || v == cartao.Cvv && v is not ("001" or "002")));

        await AssertBancoSemDadosAsync(cartao);

        AssertMetrica("bank.cartoes.emitidos", "bandeira", "001");
        AssertMetrica("bank.cobrancas.emitidas");
        AssertMetrica("bank.cobrancas.canceladas");
        AssertMetrica("bank.cobrancas.autorizacoes", "resultado", "aprovada");
        AssertMetrica("bank.cobrancas.autorizacoes", "resultado", "limite-cartao-insuficiente");
        AssertMetrica("bank.cobrancas.autorizacoes", "resultado", "cartao-nao-autorizado");
        AssertMetrica("bank.recebiveis.antecipados");

        async Task GuardarErroAsync(Task<HttpResponseMessage> requisicao, HttpStatusCode status)
        {
            using var resposta = await requisicao;
            Assert.Equal(status, resposta.StatusCode);
            corposDeErro.Add(await resposta.Content.ReadAsStringAsync(Ct));
        }
    }

    /// <summary>Procura o número em todas as colunas de todas as tabelas e o CVV como valor exato de colunas de texto.</summary>
    private async Task AssertBancoSemDadosAsync(CartaoTeste cartao)
    {
        await using var conexao = new NpgsqlConnection(factory.ConnectionString);
        await conexao.OpenAsync(Ct);
        var colunas = new List<(string Tabela, string Coluna, bool Texto)>();
        await using (var comando = new NpgsqlCommand("SELECT table_name, column_name, data_type IN ('text', 'character varying', 'character') FROM information_schema.columns WHERE table_schema = 'public'", conexao))
        await using (var leitor = await comando.ExecuteReaderAsync(Ct))
        {
            while (await leitor.ReadAsync(Ct))
            {
                colunas.Add((leitor.GetString(0), leitor.GetString(1), leitor.GetBoolean(2)));
            }
        }

        Assert.Contains(colunas, c => c is { Tabela: "cartoes", Coluna: "impressao_digital" });
        foreach (var (tabela, coluna, texto) in colunas)
        {
            await using var comando = new NpgsqlCommand(
                $"SELECT count(*) FROM \"{tabela}\" WHERE \"{coluna}\"::text LIKE '%' || @numero || '%'" +
                (texto && !CodigosDeTresDigitos.Contains(coluna) ? $" OR \"{coluna}\"::text = @cvv" : string.Empty), conexao);
            comando.Parameters.AddWithValue("numero", cartao.Numero);
            comando.Parameters.AddWithValue("cvv", cartao.Cvv);
            Assert.True((long)(await comando.ExecuteScalarAsync(Ct))! == 0, $"Dado de cartão em {tabela}.{coluna}");
        }
    }

    private static string Texto(LogRecord log) =>
        string.Join(' ', [log.FormattedMessage ?? string.Empty, log.Exception?.ToString() ?? string.Empty, .. (log.Attributes ?? []).Select(a => a.Value?.ToString() ?? string.Empty)]);

    private void AssertMetrica(string nome, string? tag = null, string? valor = null) =>
        Assert.Contains(factory.Medicoes, m => m.Nome == nome && (tag is null || m.Tags.GetValueOrDefault(tag) == valor));
}
