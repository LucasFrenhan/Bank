using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bank.Infrastructure.Mensageria;
using Bank.Infrastructure.Mensageria.Contratos;
using Bank.Infrastructure.Processamento;
using Bank.Application.Transferencias;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Leitura de <c>cobranca.paga</c> v1 (specs/014-cartao-credito-cobrancas, contracts/evento-cobranca-paga.md).</summary>
public class LeitorCobrancaPagaTests
{
    private static readonly Guid EventoId = Guid.CreateVersion7();

    private static JsonObject Completo() => new()
    {
        ["eventoId"] = EventoId,
        ["tipoEvento"] = "CobrancaPaga",
        ["versao"] = 1,
        ["cobrancaId"] = Guid.CreateVersion7(),
        ["lojistaUsuarioId"] = Guid.CreateVersion7(),
        ["pagadorUsuarioId"] = Guid.CreateVersion7(),
        ["valor"] = 299.90m,
        ["ultimosDigitosCartao"] = "7899",
        ["ocorridoEm"] = "2026-11-24T16:35:00+00:00",
        ["lojistaNome"] = "Cafeteria Ltda",
    };

    private static BasicProperties Propriedades(string? tipo = "cobranca.paga", int? versao = 1) => new()
    {
        MessageId = EventoId.ToString(),
        Type = tipo,
        Headers = versao is null ? null : new Dictionary<string, object?> { ["versao"] = versao.Value },
    };

    private static LeituraMensagem<Bank.Application.Abstracoes.NotificacaoCobranca> Ler(JsonNode? corpo, BasicProperties? propriedades = null) =>
        LeitorCobrancaPaga.Ler(Encoding.UTF8.GetBytes(corpo?.ToJsonString() ?? "null"), propriedades ?? Propriedades());

    [Fact]
    public void Completo_Valido()
    {
        var leitura = Ler(Completo());

        Assert.True(leitura.Valida);
        Assert.Equal((EventoId, 299.90m, "Cafeteria Ltda"), (leitura.Conteudo!.EventoId.Valor, leitura.Conteudo.Valor, leitura.Conteudo.LojistaNome));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LojistaNome_Opcional(bool emBranco)
    {
        var corpo = Completo();
        if (emBranco)
        {
            corpo["lojistaNome"] = " ";
        }
        else
        {
            corpo.Remove("lojistaNome");
        }

        Assert.Null(Ler(corpo).Conteudo!.LojistaNome);
    }

    [Theory]
    [InlineData("eventoId")]
    [InlineData("cobrancaId")]
    [InlineData("lojistaUsuarioId")]
    [InlineData("pagadorUsuarioId")]
    [InlineData("valor")]
    [InlineData("ocorridoEm")]
    public void CampoObrigatorioAusente_Invalida(string campo)
    {
        var corpo = Completo();
        corpo.Remove(campo);

        var leitura = Ler(corpo);

        Assert.False(leitura.Valida);
        Assert.Contains(campo, leitura.Erro, StringComparison.Ordinal);
        Assert.Equal(EventoId, leitura.EventoId);
    }

    [Fact]
    public void TipoVersaoOuCorpoErrados_Invalida()
    {
        Assert.False(Ler(Completo(), Propriedades(tipo: "transferencia.concluida")).Valida);
        Assert.False(Ler(Completo(), Propriedades(versao: 2)).Valida);
        Assert.False(Ler(null).Valida);
        var quebrado = LeitorCobrancaPaga.Ler(Encoding.UTF8.GetBytes("{ \"cartao\": 4000001234567899"), Propriedades());
        Assert.False(quebrado.Valida);
        Assert.DoesNotContain("4000001234567899", quebrado.Erro, StringComparison.Ordinal);
    }

    [Fact]
    public void EventoDoDominio_SegueOContrato()
    {
        var cartao = CartoesDeTeste.Cartao();
        var execucao = CartoesDeTeste.Pagar(CartoesDeTeste.Cobranca(), cartao, CartoesDeTeste.Fatura(cartao));
        var json = JsonDocument.Parse(CobrancaPagaV1.De(execucao.Evento).Serializar()).RootElement;

        Assert.Equal(
            ["eventoId", "tipoEvento", "versao", "cobrancaId", "lojistaUsuarioId", "pagadorUsuarioId", "valor", "ultimosDigitosCartao", "ocorridoEm", "lojistaNome"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Cafeteria Ltda", "7899"), (json.GetProperty("lojistaNome").GetString(), json.GetProperty("ultimosDigitosCartao").GetString()));
    }

    [Theory]
    [InlineData("2026-11-24T12:00:00Z", 60)]
    [InlineData("2026-11-25T02:59:30Z", 30)] // 23:59:30 de Brasília
    [InlineData("2026-11-25T03:00:00Z", 60)]
    public void Agendamento_EsperaOIntervaloOuAMeiaNoiteDeBrasilia(string agora, int segundos)
    {
        using var servico = new ProcessamentoDiarioHospedado(
            null!, Options.Create(new ProcessamentoDiarioOptions()), new CalendarioBrasilia(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProcessamentoDiarioHospedado>.Instance);

        Assert.Equal(TimeSpan.FromSeconds(segundos), servico.EsperaAteAProximaRodada(DateTimeOffset.Parse(agora, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Agendamento_IntervaloMinimo(int segundos, bool valido) =>
        Assert.Equal(valido, new ValidadorProcessamentoDiarioOptions().Validate(null, new ProcessamentoDiarioOptions { IntervaloSegundos = segundos }).Succeeded);
}
