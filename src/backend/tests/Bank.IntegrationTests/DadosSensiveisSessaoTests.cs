using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Bank.IntegrationTests;

/// <summary>
/// Sessão sem segredos na telemetria (specs/016-sessao-refresh-token, FR-020, FR-021; SC-006): login, renovação,
/// reuso e saída não expõem o cookie, o hash nem o token em logs ou spans, e publicam as métricas da sessão.
/// </summary>
public class DadosSensiveisSessaoTests(TelemetriaCompletaApiFactory factory) : IClassFixture<TelemetriaCompletaApiFactory>
{
    [Fact]
    public async Task LoginRenovacaoReusoESaida_SemSegredosNaTelemetria_ComMetricas()
    {
        using var client = factory.CreateClient();
        factory.IniciarCapturaDeMetricas();

        var (email, login) = await ApiSessao.CadastrarEEntrarAsync(client);
        var renovada = await ApiSessao.RenovarComSucessoAsync(client, login.Cookie.Valor);
        var segundoNavegador = await ApiSessao.EntrarAsync(client, email);
        var novoLogin = await ApiSessao.EntrarAsync(client, email, segundoNavegador.Cookie.Valor);
        (await ApiSessao.SairAsync(client, renovada.Cookie.Valor)).Dispose();
        (await ApiSessao.RenovarAsync(client, "credencial-inventada")).Dispose();

        factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
        factory.Services.GetRequiredService<LoggerProvider>().ForceFlush();

        var textos = Textos();
        Assert.NotEmpty(textos);
        string[] sensiveis =
        [
            login.Cookie.Valor, renovada.Cookie.Valor, segundoNavegador.Cookie.Valor, novoLogin.Cookie.Valor,
            Convert.ToHexString(RenovacaoTests.Hash(login.Cookie.Valor).Bytes),
            login.Token, login.Token.Split('.')[1], renovada.Token.Split('.')[1],
            CookieDeTeste.Nome + "=",
        ];
        foreach (var sensivel in sensiveis)
        {
            Assert.DoesNotContain(textos, t => t.Contains(sensivel, StringComparison.OrdinalIgnoreCase));
        }

        AssertMetrica("bank.autenticacao.renovacoes", "resultado", "sucesso");
        AssertMetrica("bank.autenticacao.renovacoes", "resultado", "recusada");
        AssertMetrica("bank.autenticacao.sessoes_revogadas", "motivo", "logout");
        AssertMetrica("bank.autenticacao.sessoes_revogadas", "motivo", "novo-login");
    }

    private List<string> Textos()
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

    private void AssertMetrica(string nome, string tag, string valor) =>
        Assert.Contains(factory.Medicoes, m => m.Nome == nome && m.Tags.TryGetValue(tag, out var v) && v == valor && m.Valor > 0);
}
