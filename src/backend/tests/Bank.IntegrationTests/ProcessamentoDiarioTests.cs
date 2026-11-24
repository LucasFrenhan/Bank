using Bank.Application.Processamento;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Lançamento e liberação de faturas (specs/014-cartao-credito-cobrancas, US4; FR-022 a FR-026; SC-005, SC-006). Cada
/// teste usa um ano próprio, porque a fábrica é compartilhada.
/// </summary>
public class ProcessamentoDiarioTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>, IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        factory.Relogio.Agora = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CompraDeHoje_SoELancadaNoProximoDiaUtil()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2027, 11, 24, 10); // quarta-feira
        await ComprarAsync(client, cartao, pj);
        await factory.ProcessarAsync();

        var antes = await FaturaAsync(client, cartao, "2027-12");
        Assert.Equal(("Aberta", 0m, 0), (antes.GetProperty("situacao").GetString(), antes.GetProperty("total").GetDecimal(), antes.GetProperty("lancamentos").GetArrayLength()));
        Assert.Equal(4700.10m, await LimiteDisponivelAsync(client, cartao));

        await factory.ProcessarEmAsync(Brasilia(2027, 11, 25, 0, 0, 0));

        var depois = await FaturaAsync(client, cartao, "2027-12");
        Assert.Equal(("Aberta", 299.90m), (depois.GetProperty("situacao").GetString(), depois.GetProperty("total").GetDecimal()));
        var item = Assert.Single(depois.GetProperty("lancamentos").EnumerateArray());
        Assert.Equal(("Empresa Exemplo Ltda", "Café Especial", 299.90m, "2027-11-24"),
            (item.GetProperty("estabelecimento").GetString(), item.GetProperty("descricao").GetString(), item.GetProperty("valor").GetDecimal(), item.GetProperty("dataCompra").GetString()));
        Assert.Equal(0m, (await GetOkAsync(client, cartao.Conta.Token, MeuCartao)).GetProperty("comprasALancar").GetDecimal());
        Assert.Equal(4700.10m, await LimiteDisponivelAsync(client, cartao));
    }

    [Fact]
    public async Task ViradaDoDia07_DefineOCicloELiberaSoDepoisDeLancar()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2026, 12, 7, 23, 59, 59); // segunda-feira
        await ComprarAsync(client, cartao, pj, 100m);
        factory.Relogio.Agora = Brasilia(2026, 12, 8, 0, 0, 0);
        await ComprarAsync(client, cartao, pj, 50m);

        var fechada = await FaturaAsync(client, cartao, "2026-12");
        Assert.Equal("Fechada", fechada.GetProperty("situacao").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, fechada.GetProperty("valorAPagar").ValueKind);

        await factory.ProcessarAsync();

        var dezembro = await FaturaAsync(client, cartao, "2026-12");
        Assert.Equal(("Disponivel", 100m, 100m, "2026-12-10", "2026-12-07", "2026-11-08"),
            (dezembro.GetProperty("situacao").GetString(), dezembro.GetProperty("total").GetDecimal(), dezembro.GetProperty("valorAPagar").GetDecimal(),
             dezembro.GetProperty("vencimento").GetString(), dezembro.GetProperty("fechamento").GetString(), dezembro.GetProperty("inicio").GetString()));

        await factory.ProcessarEmAsync(Brasilia(2026, 12, 9, 0, 0, 0));
        var janeiro = await FaturaAsync(client, cartao, "2027-01");
        Assert.Equal(("Aberta", 50m, "2027-01-11"), (janeiro.GetProperty("situacao").GetString(), janeiro.GetProperty("total").GetDecimal(), janeiro.GetProperty("vencimento").GetString()));
    }

    [Fact]
    public async Task DiasNaoUteis_NadaELancado_SextaVaiParaSegunda()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2027, 5, 7, 18); // sexta-feira
        await ComprarAsync(client, cartao, pj, 100m);

        Assert.Equal(ResumoProcessamento.Vazio, await factory.ProcessarEmAsync(Brasilia(2027, 5, 8, 0, 0, 0)));
        Assert.Equal(ResumoProcessamento.Vazio, await factory.ProcessarEmAsync(Brasilia(2027, 5, 9, 12)));
        Assert.Equal(("Fechada", 0m), Resumo(await FaturaAsync(client, cartao, "2027-05")));

        await factory.ProcessarEmAsync(Brasilia(2027, 5, 10, 0, 0, 0));

        var liberada = await FaturaAsync(client, cartao, "2027-05");
        Assert.Equal(("Disponivel", 100m), Resumo(liberada));
        Assert.Equal("2027-05-10", liberada.GetProperty("vencimento").GetString());
    }

    [Fact]
    public async Task CompraNoDomingoDia07_EntraNaFaturaDoMes()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2027, 3, 7, 20); // domingo
        await ComprarAsync(client, cartao, pj, 100m);

        await factory.ProcessarEmAsync(Brasilia(2027, 3, 8, 0, 0, 0));

        Assert.Equal(("Disponivel", 100m), Resumo(await FaturaAsync(client, cartao, "2027-03")));
    }

    [Fact]
    public async Task Feriado_NadaELancado()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2028, 4, 20, 10); // quinta-feira, véspera de Tiradentes
        await ComprarAsync(client, cartao, pj, 100m);

        Assert.Equal(ResumoProcessamento.Vazio, await factory.ProcessarEmAsync(Brasilia(2028, 4, 21, 12)));
        await factory.ProcessarEmAsync(Brasilia(2028, 4, 24, 0, 0, 0));

        Assert.Equal(100m, (await FaturaAsync(client, cartao, "2028-05")).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task RepetidoEAtrasado_CadaCompraUmaVez_NoCicloCerto()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        foreach (var dia in new[] { 1, 5, 7 })
        {
            factory.Relogio.Agora = Brasilia(2029, 2, dia, 10);
            await ComprarAsync(client, cartao, pj, 10m);
        }

        factory.Relogio.Agora = Brasilia(2029, 2, 8, 10);
        await ComprarAsync(client, cartao, pj, 1m);

        // Parado por dias: só volta em 20/02 (terça-feira), depois do vencimento (12/02).
        await factory.ProcessarEmAsync(Brasilia(2029, 2, 20, 9));
        await factory.ProcessarAsync();
        await factory.ProcessarEmAsync(Brasilia(2029, 2, 21, 9));

        var fevereiro = await FaturaAsync(client, cartao, "2029-02");
        Assert.Equal(30m, fevereiro.GetProperty("total").GetDecimal());
        Assert.Equal(3, fevereiro.GetProperty("lancamentos").GetArrayLength());
        Assert.Equal("2029-02-20", fevereiro.GetProperty("vencimento").GetString()); // vence no dia da liberação
        Assert.Equal((1, 0.15m), (fevereiro.GetProperty("diasAtraso").GetInt32(), fevereiro.GetProperty("juros").GetDecimal()));
        Assert.Equal(("Aberta", 1m), Resumo(await FaturaAsync(client, cartao, "2029-03")));
        await AssertCadaCompraLancadaUmaVezAsync();
    }

    [Fact]
    public async Task LiberadaComAtraso_NoDiaDaLiberacao_SemJurosNemBloqueio()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2030, 2, 1, 10);
        await ComprarAsync(client, cartao, pj, 100m);

        await factory.ProcessarEmAsync(Brasilia(2030, 2, 20, 9));

        var fatura = await FaturaAsync(client, cartao, "2030-02");
        Assert.Equal(("Disponivel", 0m, 100m), (fatura.GetProperty("situacao").GetString(), fatura.GetProperty("juros").GetDecimal(), fatura.GetProperty("valorAPagar").GetDecimal()));
        await ComprarAsync(client, cartao, pj, 1m);
    }

    [Fact]
    public async Task ExecucoesSimultaneas_NaoDuplicam()
    {
        using var client = factory.CreateClient();
        var pj = await CriarLojistaAsync(factory, client);
        var cartoes = new List<CartaoTeste>();
        for (var i = 0; i < 5; i++)
        {
            cartoes.Add(await CriarPfComCartaoAsync(factory, client));
        }

        factory.Relogio.Agora = Brasilia(2031, 2, 3, 10);
        foreach (var cartao in cartoes)
        {
            await ComprarAsync(client, cartao, pj, 10m);
            await ComprarAsync(client, cartao, pj, 5m);
        }

        factory.Relogio.Agora = Brasilia(2031, 2, 4, 0, 0, 0);
        var resumos = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => factory.ProcessarAsync()));

        Assert.All(resumos, r => Assert.Equal(0, r.Falhas));
        foreach (var cartao in cartoes)
        {
            Assert.Equal(15m, (await FaturaAsync(client, cartao, "2031-02")).GetProperty("total").GetDecimal());
        }

        await AssertCadaCompraLancadaUmaVezAsync();
    }

    [Fact]
    public async Task CompraNaoConfirmada_SeguraOCartao_EEntraNaFatura()
    {
        using var client = factory.CreateClient();
        var (pj, cartao) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2028, 9, 7, 23, 59, 0); // quinta-feira (feriado; o dia 8 é útil)
        await ComprarAsync(client, cartao, pj, 100m);
        var cobranca = await EmitirAsync(client, pj, 50m);

        // Segura a gravação do pagamento das 23:59:59 com um bloqueio na tabela de recebíveis.
        await using var conexao = new NpgsqlConnection(factory.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var trava = await conexao.BeginTransactionAsync(Ct);
        await using (var comando = new NpgsqlCommand("LOCK TABLE recebiveis IN SHARE ROW EXCLUSIVE MODE", conexao, trava))
        {
            await comando.ExecuteNonQueryAsync(Ct);
        }

        factory.Relogio.Agora = Brasilia(2028, 9, 7, 23, 59, 59);
        var pagamento = PagarAsync(client, cartao, cobranca);
        await AguardarBloqueioAsync(conexao, trava);

        factory.Relogio.Agora = Brasilia(2028, 9, 8, 0, 0, 0);
        var processamento = factory.ProcessarAsync();
        await Task.Delay(300, Ct);
        Assert.False(processamento.IsCompleted, "O processamento deveria esperar o cartão bloqueado.");

        await trava.CommitAsync(Ct);
        using var resposta = await pagamento;
        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        await processamento;

        Assert.Equal(("Disponivel", 150m), Resumo(await FaturaAsync(client, cartao, "2028-09")));
    }

    [Fact]
    public async Task FalhaEmUmCartao_NaoParaOsDemais_EELancadoDepois()
    {
        using var client = factory.CreateClient();
        var (pj, comFalha) = await ContasAsync(client);
        var (_, semFalha) = await ContasAsync(client);
        factory.Relogio.Agora = Brasilia(2027, 8, 6, 10); // sexta-feira
        await ComprarAsync(client, comFalha, pj, 10m, "FALHAR");
        await ComprarAsync(client, semFalha, pj, 20m);
        await using var contexto = factory.CriarContexto();
        await contexto.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION falhar_lancamento_de_teste() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'falha de teste'; END; $$;
            CREATE TRIGGER tg_falha_de_teste BEFORE INSERT ON lancamentos_fatura FOR EACH ROW WHEN (NEW.descricao = 'FALHAR') EXECUTE FUNCTION falhar_lancamento_de_teste();
            """, Ct);

        try
        {
            var resumo = await factory.ProcessarEmAsync(Brasilia(2027, 8, 9, 0, 0, 0));

            Assert.True(resumo.Falhas >= 1);
            Assert.Equal(("Fechada", 0m), Resumo(await FaturaAsync(client, comFalha, "2027-08")));
            Assert.Equal(("Disponivel", 20m), Resumo(await FaturaAsync(client, semFalha, "2027-08")));
        }
        finally
        {
            await contexto.Database.ExecuteSqlRawAsync("DROP TRIGGER tg_falha_de_teste ON lancamentos_fatura; DROP FUNCTION falhar_lancamento_de_teste();", Ct);
        }

        Assert.Equal(0, (await factory.ProcessarAsync()).Falhas);
        Assert.Equal(("Disponivel", 10m), Resumo(await FaturaAsync(client, comFalha, "2027-08")));
    }

    private async Task<(ContaTeste Lojista, CartaoTeste Cartao)> ContasAsync(HttpClient client) =>
        (await CriarLojistaAsync(factory, client), await CriarPfComCartaoAsync(factory, client));

    private static (string? Situacao, decimal Total) Resumo(System.Text.Json.JsonElement fatura) =>
        (fatura.GetProperty("situacao").GetString(), fatura.GetProperty("total").GetDecimal());

    private static async Task AguardarBloqueioAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao)
    {
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            await using var comando = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE NOT granted AND relation = 'recebiveis'::regclass", conexao, transacao);
            return (long)(await comando.ExecuteScalarAsync(Ct))! > 0;
        }), "O pagamento não chegou à gravação.");
    }

    /// <summary>Compras de faturas liberadas lançadas exatamente uma vez, na própria fatura, e totais coerentes (SC-005, SC-006).</summary>
    private async Task AssertCadaCompraLancadaUmaVezAsync()
    {
        await using var contexto = factory.CriarContexto();
        Assert.Equal(0, await contexto.ComprasCartao.CountAsync(
            c => contexto.Faturas.Any(f => f.Id == c.FaturaId && f.LiberadaEm != null) && contexto.LancamentosFatura.Count(l => l.CompraId == c.Id && l.FaturaId == c.FaturaId) != 1, Ct));
        Assert.Equal(0, await contexto.Faturas.CountAsync(
            f => f.Total != (contexto.LancamentosFatura.Where(l => l.FaturaId == f.Id).Sum(l => (decimal?)l.Valor) ?? 0m), Ct));
    }
}

/// <summary>Serviço hospedado do Worker (specs/014-cartao-credito-cobrancas, FR-022; research R7, R17).</summary>
public class ProcessamentoHospedadoTests(AmbienteProcessamento ambiente) : IClassFixture<AmbienteProcessamento>
{
    [Fact]
    public async Task Ligado_LancaNaPartida()
    {
        using var client = ambiente.Api.CreateClient();
        var pj = await CriarLojistaAsync(ambiente.Api, client);
        var cartao = await CriarPfComCartaoAsync(ambiente.Api, client);
        ambiente.Api.Relogio.Agora = Brasilia(2027, 11, 24, 10);
        await ComprarAsync(client, cartao, pj, 10m);

        ambiente.Api.Relogio.Agora = Brasilia(2027, 11, 25, 0, 0, 1);
        ambiente.IniciarWorker();

        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
            (await FaturaAsync(client, cartao, "2027-12")).GetProperty("total").GetDecimal() == 10m), "A compra não foi lançada pelo Worker.");

        // Segunda rodada, pelo intervalo.
        await ComprarAsync(client, cartao, pj, 5m);
        ambiente.Api.Relogio.Agora = Brasilia(2027, 11, 26, 0, 0, 1);
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
            (await FaturaAsync(client, cartao, "2027-12")).GetProperty("total").GetDecimal() == 15m), "A segunda rodada não lançou a compra.");

        Assert.Contains(ambiente.Worker.Spans.Copiar(), s => s.OperationName == ProcessarDia.NomeSpan);
        Assert.True(ambiente.Worker.Somar("bank.processamento.execucoes", "resultado", "concluida") >= 2);
        Assert.Equal(2, ambiente.Worker.Somar("bank.processamento.compras_lancadas"));
        Assert.True(ambiente.Worker.Somar("bank.processamento.duracao") >= 0);
    }
}

/// <summary>API com relógio ajustável e Worker com o processamento ligado, no mesmo relógio.</summary>
public sealed class AmbienteProcessamento : IAsyncLifetime
{
    private BankWorkerFactory? _worker;
    private BrokerDeTeste? _broker;

    public CartoesApiFactory Api { get; } = new();

    public BankWorkerFactory Worker => _worker ?? throw new InvalidOperationException("O Worker não foi iniciado.");

    public async ValueTask InitializeAsync()
    {
        await Api.InitializeAsync();
        _ = Api.Server;
        _broker = await RabbitMqCompartilhado.CriarVirtualHostAsync();
    }

    public void IniciarWorker()
    {
        _worker = new BankWorkerFactory(Api.ConnectionString, _broker!, servicos =>
        {
            servicos.AddSingleton<TimeProvider>(Api.Relogio);
            servicos.PostConfigure<Bank.Infrastructure.Processamento.ProcessamentoDiarioOptions>(o =>
            {
                o.Habilitado = true;
                o.IntervaloSegundos = 1;
            });
        });
        _worker.Iniciar();
    }

    public async ValueTask DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.DisposeAsync();
        }

        await Api.DisposeAsync();
    }
}
