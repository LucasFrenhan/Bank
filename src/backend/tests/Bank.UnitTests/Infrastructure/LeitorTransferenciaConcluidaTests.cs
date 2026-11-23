using System.Text;
using Bank.Infrastructure.Mensageria;
using RabbitMQ.Client;

namespace Bank.UnitTests.Infrastructure;

/// <summary>Leitura do contrato v1 pelo consumidor (specs/010-mensageria-rabbitmq, FR-011, FR-018b).</summary>
public class LeitorTransferenciaConcluidaTests
{
    private static readonly Guid EventoId = Guid.CreateVersion7();

    private static string Completo(Guid? eventoId = null) => $$"""
        {"eventoId":"{{eventoId ?? EventoId}}","tipoEvento":"TransferenciaConcluida","versao":1,
         "transferenciaId":"019a8f55-2222-7890-abcd-ef1234567890","pagadorUsuarioId":"019a8f12-3456-7890-abcd-ef1234567890",
         "pagadorNome":"Lucas Silva","recebedorUsuarioId":"019a8f99-8888-7890-abcd-ef1234567890",
         "recebedorNome":"Cafeteria","valor":150.50,"descricao":null,"ocorridoEm":"2026-11-17T15:00:00+00:00"}
        """;

    /// <summary>Só os 6 campos da v1 original (eventos gravados antes da spec 010).</summary>
    private static readonly string Antigo = $$"""
        {"eventoId":"{{EventoId}}","transferenciaId":"019a8f55-2222-7890-abcd-ef1234567890",
         "pagadorUsuarioId":"019a8f12-3456-7890-abcd-ef1234567890","recebedorUsuarioId":"019a8f99-8888-7890-abcd-ef1234567890",
         "valor":1.00,"ocorridoEm":"2026-11-17T15:00:00+00:00"}
        """;

    private static BasicProperties Propriedades(string? tipo = "transferencia.concluida", object? versao = null, bool semVersao = false) => new()
    {
        MessageId = EventoId.ToString(),
        Type = tipo,
        Headers = semVersao ? new Dictionary<string, object?>() : new Dictionary<string, object?> { ["versao"] = versao ?? 1 },
    };

    private static LeituraMensagem<Bank.Application.Abstracoes.NotificacaoTransferencia> Ler(string corpo, BasicProperties? propriedades = null) =>
        LeitorTransferenciaConcluida.Ler(Encoding.UTF8.GetBytes(corpo), propriedades ?? Propriedades());

    [Fact]
    public void Completo_Valido()
    {
        var leitura = Ler(Completo());

        Assert.True(leitura.Valida);
        Assert.Equal(EventoId, leitura.EventoId);
        var n = leitura.Conteudo!;
        Assert.Equal((EventoId, 150.50m), (n.EventoId.Valor, n.Valor));
        Assert.Equal(Guid.Parse("019a8f12-3456-7890-abcd-ef1234567890"), n.PagadorUsuarioId.Valor);
        Assert.Equal(Guid.Parse("019a8f99-8888-7890-abcd-ef1234567890"), n.RecebedorUsuarioId.Valor);
    }

    [Fact]
    public void Antigo_SemCamposNovos_Valido()
    {
        Assert.True(Ler(Antigo).Valida);
    }

    [Fact]
    public void Completo_LeOsNomes()
    {
        var n = Ler(Completo()).Conteudo!;

        Assert.Equal(("Lucas Silva", "Cafeteria"), (n.PagadorNome, n.RecebedorNome));
    }

    [Fact]
    public void Antigo_SemNomes_NomesNulos()
    {
        var leitura = Ler(Antigo);

        Assert.True(leitura.Valida);
        Assert.Equal((null, null), (leitura.Conteudo!.PagadorNome, leitura.Conteudo.RecebedorNome));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void NomeNuloOuEmBranco_Nulo_EMensagemValida(string nomeJson)
    {
        var corpo = Completo()
            .Replace("\"Lucas Silva\"", nomeJson, StringComparison.Ordinal)
            .Replace("\"Cafeteria\"", nomeJson, StringComparison.Ordinal);

        var leitura = Ler(corpo);

        Assert.True(leitura.Valida);
        Assert.Equal((null, null), (leitura.Conteudo!.PagadorNome, leitura.Conteudo.RecebedorNome));
    }

    [Fact]
    public void CampoDesconhecido_Ignorado()
    {
        Assert.True(Ler(Completo().Replace("\"valor\"", "\"campoNovo\":42,\"valor\"", StringComparison.Ordinal)).Valida);
    }

    [Fact]
    public void VersaoComoTextoNoHeader_Valida()
    {
        Assert.True(Ler(Completo(), Propriedades(versao: Encoding.UTF8.GetBytes("1"))).Valida);
    }

    [Fact]
    public void VersaoSoNoCorpo_Valida()
    {
        Assert.True(Ler(Completo(), Propriedades(semVersao: true)).Valida);
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{\"eventoId\": \"x\"}")]
    [InlineData("null")]
    public void CorpoInvalido_Invalido(string corpo)
    {
        var leitura = Ler(corpo);

        Assert.False(leitura.Valida);
        Assert.NotNull(leitura.Erro);
    }

    [Theory]
    [InlineData("eventoId")]
    [InlineData("transferenciaId")]
    [InlineData("pagadorUsuarioId")]
    [InlineData("recebedorUsuarioId")]
    [InlineData("valor")]
    [InlineData("ocorridoEm")]
    public void CampoObrigatorioAusente_Invalido(string campo)
    {
        var corpo = System.Text.Json.Nodes.JsonNode.Parse(Completo())!.AsObject();
        corpo.Remove(campo);

        var leitura = Ler(corpo.ToJsonString());

        Assert.False(leitura.Valida);
        Assert.Contains(campo, leitura.Erro, StringComparison.Ordinal);
    }

    [Fact]
    public void TipoDiferente_Invalido()
    {
        Assert.False(Ler(Completo(), Propriedades(tipo: "cobranca.paga")).Valida);
        Assert.False(Ler(Completo(), Propriedades(tipo: null)).Valida);
    }

    [Fact]
    public void VersaoDesconhecida_Invalida()
    {
        var leitura = Ler(Completo(), Propriedades(versao: 2));

        Assert.False(leitura.Valida);
        Assert.Equal(EventoId, leitura.EventoId);
    }
}
