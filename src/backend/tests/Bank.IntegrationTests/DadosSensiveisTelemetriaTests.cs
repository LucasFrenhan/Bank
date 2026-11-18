using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Bank.IntegrationTests;

/// <summary>
/// Fábrica que captura em memória logs e spans exportados pela API e, com um
/// <see cref="MeterListener"/>, cada medição dos meters da API no momento em que é registrada.
/// </summary>
/// <remarks>
/// As métricas não são lidas pelo exportador em memória do SDK: o que ele entrega depende de
/// quando o leitor coleta, e no CI a métrica de login recusado chegou a faltar (PR #35).
/// </remarks>
public sealed class TelemetriaCompletaApiFactory : BankApiFactory
{
    private readonly Lock _trava = new();
    private MeterListener? _ouvinte;

    public SynchronizedCollection<Activity> Spans { get; } = new();

    public SynchronizedCollection<LogRecord> Logs { get; } = new();

    /// <summary>Medições (instrumento, valor, tags) dos meters desta API.</summary>
    public ConcurrentQueue<(string Nome, double Valor, Dictionary<string, string?> Tags)> Medicoes { get; } = new();

    /// <summary>
    /// Passa a ouvir os meters criados pelo <see cref="IMeterFactory"/> desta API; o escopo do
    /// meter separa esta fábrica das outras que rodam em paralelo no mesmo processo.
    /// </summary>
    public void IniciarCapturaDeMetricas()
    {
        lock (_trava)
        {
            if (_ouvinte is not null)
            {
                return;
            }

            var fabricaDeMeters = Services.GetRequiredService<IMeterFactory>();
            _ouvinte = new MeterListener
            {
                InstrumentPublished = (instrumento, ouvinte) =>
                {
                    if (ReferenceEquals(instrumento.Meter.Scope, fabricaDeMeters))
                    {
                        ouvinte.EnableMeasurementEvents(instrumento);
                    }
                },
            };
            _ouvinte.SetMeasurementEventCallback<long>((i, v, tags, _) => Registrar(i.Name, v, tags));
            _ouvinte.SetMeasurementEventCallback<double>((i, v, tags, _) => Registrar(i.Name, v, tags));
            _ouvinte.Start();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _ouvinte?.Dispose();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(Spans));
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddInMemoryExporter(Logs));
        });
    }

    private void Registrar(string nome, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        Medicoes.Enqueue((nome, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString())));
}

/// <summary>
/// SC-006 / FR-025: senha, token, documento e e-mail nunca aparecem em logs ou traces; FR-027:
/// métricas de negócio publicadas com as tags corretas (research R9).
/// </summary>
public class DadosSensiveisTelemetriaTests(TelemetriaCompletaApiFactory factory) : IClassFixture<TelemetriaCompletaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CadastroLoginEConsulta_NaoExpoemDadosSensiveisETemMetricas()
    {
        using var client = factory.CreateClient();
        factory.IniciarCapturaDeMetricas();
        const string senha = "Sigilosa#2026xyz";
        var email = Dados.Email();
        var cnpj = Dados.CnpjAlfanumerico();
        var cadastro = new { nomeCompleto = "Empresa Sigilosa", email, documento = Dados.CnpjComMascara(cnpj), tipoConta = "PessoaJuridica", senha };

        (await client.PostAsJsonAsync(ApiBank.Registrar, cadastro, Ct)).Dispose();
        (await client.PostAsJsonAsync(ApiBank.Login, new { email, senha = "Errada#2026xyz" }, Ct)).Dispose();
        var token = await ApiBank.ObterTokenAsync(client, email, senha);
        using (var requisicao = new HttpRequestMessage(HttpMethod.Get, ApiBank.MinhaCarteira))
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await client.SendAsync(requisicao, Ct)).Dispose();
        }

        // specs/005-usuarios-me: a consulta dos dados do titular também não deixa rastro sensível.
        using (var requisicao = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/usuarios/me", UriKind.Relative)))
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resposta = await client.SendAsync(requisicao, Ct);
            Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        }

        ForcarExportacao();

        var textos = TextosExportados();
        Assert.NotEmpty(textos);
        string[] sensiveis = [senha, "Errada#2026xyz", token, token.Split('.')[1], cnpj, Dados.CnpjComMascara(cnpj), email];
        foreach (var sensivel in sensiveis)
        {
            Assert.DoesNotContain(textos, t => t.Contains(sensivel, StringComparison.OrdinalIgnoreCase));
        }

        AssertMetrica("bank.usuarios.cadastros", "tipo_conta", "PessoaJuridica");
        AssertMetrica("bank.autenticacao.logins", "resultado", "sucesso");
        AssertMetrica("bank.autenticacao.logins", "resultado", "falha");
    }

    /// <summary>Spec 006 (FR-009b, FR-020): transferência, consulta de destinatário e extrato.</summary>
    [Fact]
    public async Task Transferencias_NaoExpoemDadosSensiveisETemMetricas()
    {
        using var client = factory.CreateClient();
        factory.IniciarCapturaDeMetricas();
        var emailPagador = Dados.Email();
        var cpfPagador = Dados.Cpf();
        (await client.PostAsJsonAsync(ApiBank.Registrar, new { nomeCompleto = "Pagadora Reservada", email = emailPagador, documento = cpfPagador, tipoConta = "PessoaFisica", senha = Dados.SenhaValida }, Ct)).Dispose();
        var emailRecebedor = Dados.Email();
        var cnpjRecebedor = Dados.CnpjAlfanumerico();
        (await client.PostAsJsonAsync(ApiBank.Registrar, new { nomeCompleto = "Recebedora Reservada Ltda", email = emailRecebedor, documento = cnpjRecebedor, tipoConta = "PessoaJuridica", senha = Dados.SenhaValida }, Ct)).Dispose();
        var token = await ApiBank.ObterTokenAsync(client, emailPagador);
        var pagador = new ContaTeste(emailPagador, cpfPagador, Guid.Empty, Guid.Empty, token);

        using (var consulta = await ApiTransferencias.PostComTokenAsync(client, token, ApiTransferencias.ConsultarDestinatario, new { documento = cnpjRecebedor }))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, consulta.StatusCode);
        }

        Guid transferenciaId;
        using (var transferencia = await ApiTransferencias.TransferirAsync(client, pagador, Dados.CnpjComMascara(cnpjRecebedor), 2m))
        {
            Assert.Equal(System.Net.HttpStatusCode.Created, transferencia.StatusCode);
            transferenciaId = (await ApiBank.LerJsonAsync(transferencia)).GetProperty("transferenciaId").GetGuid();
        }

        // Consulta por identificador (spec 008): pagador, recebedor, terceiro e identificador inválido.
        var tokenRecebedor = await ApiBank.ObterTokenAsync(client, emailRecebedor);
        var terceiro = await ApiTransferencias.CriarPfAsync(client);
        foreach (var (tokenConsulta, id) in new[] { (token, transferenciaId.ToString()), (tokenRecebedor, transferenciaId.ToString()), (terceiro.Token, transferenciaId.ToString()), (token, "abc") })
        {
            (await ApiTransferencias.GetComTokenAsync(client, tokenConsulta, $"/api/v1/transferencias/{id}")).Dispose();
        }

        (await ApiTransferencias.TransferirAsync(client, pagador, cnpjRecebedor, 100m)).Dispose();
        (await ApiTransferencias.GetComTokenAsync(client, token, "/api/v1/transferencias/extrato")).Dispose();

        ForcarExportacao();

        var textos = TextosExportados();
        string[] sensiveis = [cpfPagador, Dados.CpfComMascara(cpfPagador), cnpjRecebedor, Dados.CnpjComMascara(cnpjRecebedor), emailPagador, emailRecebedor, "Pagadora Reservada", "Recebedora Reservada", token];
        foreach (var sensivel in sensiveis)
        {
            Assert.DoesNotContain(textos, t => t.Contains(sensivel, StringComparison.OrdinalIgnoreCase));
        }

        AssertMetrica("bank.transferencias.concluidas", "tipo_destino", "PessoaJuridica");
        AssertMetrica("bank.transferencias.recusadas", "motivo", "saldo-insuficiente");
        AssertMetrica("bank.transferencias.destinatario.consultas", "resultado", "encontrado");

        // Spec 008, FR-007: consultas por resultado, só com o rótulo "resultado" (nunca o identificador).
        foreach (var resultado in new[] { "encontrada", "nao-encontrada", "invalida" })
        {
            AssertMetrica("bank.transferencias.consultas", "resultado", resultado);
        }

        Assert.All(factory.Medicoes.Where(m => m.Nome == "bank.transferencias.consultas"), m => Assert.Equal(["resultado"], m.Tags.Keys));
    }

    private void ForcarExportacao()
    {
        factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
        factory.Services.GetRequiredService<LoggerProvider>().ForceFlush();
    }

    private List<string> TextosExportados()
    {
        var textos = new List<string>();

        foreach (var log in factory.Logs.Copiar())
        {
            textos.Add(log.FormattedMessage ?? string.Empty);
            textos.Add(log.Body ?? string.Empty);
            textos.Add(log.Exception?.ToString() ?? string.Empty);
            textos.AddRange(log.Attributes?.Select(a => $"{a.Key}={a.Value}") ?? []);
        }

        foreach (var span in factory.Spans.Copiar())
        {
            textos.Add(span.DisplayName);
            textos.AddRange(span.TagObjects.Select(t => $"{t.Key}={t.Value}"));
        }

        return textos;
    }

    private void AssertMetrica(string nome, string tag, string valor)
    {
        Assert.Contains(factory.Medicoes, m => m.Nome == nome && m.Tags.TryGetValue(tag, out var v) && v == valor && m.Valor > 0);
    }
}
