using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

/// <summary>Caso de uso de transferência (spec 006: US1, US2; research R3, R4).</summary>
public sealed class RealizarTransferenciaTests : IDisposable
{
    private static readonly NomesTitulares Nomes = new("Pagador", "Recebedor");

    private readonly BancoFalso _banco = new();
    private readonly MetricasCapturadasTransferencias _metricas = new();
    private readonly RealizarTransferencia _casoDeUso;
    private readonly Usuario _pf;
    private readonly Usuario _pj;

    /// <summary>Sem limites por padrão; os testes da spec 009 configuram os valores.</summary>
    private readonly LimitesTransferenciaOptions _limites = new();
    private readonly LoggerCapturado<RealizarTransferencia> _logger = new();

    /// <summary>23h30 de 17/11 em Brasília (02h30 UTC de 18/11): o dia do limite é 17/11.</summary>
    private static readonly RelogioFixo Relogio = new(new DateTimeOffset(2026, 11, 18, 2, 30, 0, TimeSpan.Zero));

    public RealizarTransferenciaTests()
    {
        _casoDeUso = new RealizarTransferencia(_banco, _banco, _banco, _banco, new MonitorDeOpcoes<LimitesTransferenciaOptions>(_limites), new CalendarioBrasilia(), Relogio, _metricas.Metricas, _logger);
        _pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
        _pj = _banco.Criar("Cafeteria Ltda", "12ABC34501DE35", TipoConta.PessoaJuridica, 0m);
    }

    public void Dispose() => _metricas.Dispose();

    [Fact]
    public async Task Executar_Sucesso_DebitaCreditaEDevolveComprovante()
    {
        var resultado = await Transferir("12.abc.345/01de-35", 4.5m, "  Almoço ");

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor.Replay);
        var comprovante = resultado.Valor.Comprovante;
        Assert.Equal(("Lucas Silva", "Cafeteria Ltda", "12ABC34501DE35", 4.5m, "Almoço", StatusTransferencia.Concluida),
            (comprovante.Pagador.Nome, comprovante.Recebedor.Nome, comprovante.Recebedor.Documento, comprovante.Valor, comprovante.Descricao, comprovante.Status));
        Assert.Equal((5.5m, 4.5m), (_pf.Carteira.Saldo, _pj.Carteira.Saldo));
        // Spec 008, FR-002a: documento do pagador mascarado; chave sempre preenchida no envio.
        Assert.Equal("•••.982.247-••", comprovante.Pagador.Documento);
        Assert.NotNull(comprovante.ChaveIdempotencia);
        var evento = Assert.Single(_banco.Gravadas).Evento;
        // Spec 010, FR-011: o evento leva os nomes lidos pelo caso de uso e a descrição normalizada.
        Assert.Equal(("Lucas Silva", "Cafeteria Ltda", "Almoço"), (evento.PagadorNome, evento.RecebedorNome, evento.Descricao));
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.concluidas" && m.Tags["tipo_destino"] == "PessoaJuridica");
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.valor");
    }

    [Fact]
    public async Task Executar_TodosOsCamposInvalidos_DevolveTodosOsErros()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new RealizarTransferenciaComando(_pf.Id, "nao-e-uuid", "123", 10.005m, new string('a', 201)), CancellationToken.None);

        Assert.Equal(["Idempotency-Key", "descricao", "documentoDestino", "valor"], resultado.Erros.Select(e => e.Campo!).Order(StringComparer.Ordinal));
        AssertRecusa("validacao");
    }

    [Fact]
    public async Task Executar_DestinoInexistente_404()
    {
        var resultado = await Transferir("11222333000181", 1m);

        Assert.Equal(ErrosTransferencia.DestinatarioNaoEncontrado, Assert.Single(resultado.Erros));
        AssertRecusa("destinatario-nao-encontrado");
    }

    [Fact]
    public async Task Executar_ParaOProprioDocumento_Autotransferencia()
    {
        var resultado = await Transferir("529.982.247-25", 1m);

        Assert.Equal(ErrosTransferencia.Autotransferencia, Assert.Single(resultado.Erros));
    }

    [Fact]
    public async Task Executar_SaldoInsuficiente_RecusaSemGravar()
    {
        var resultado = await Transferir("12ABC34501DE35", 10.01m);

        Assert.Equal(ErrosTransferencia.SaldoInsuficiente, Assert.Single(resultado.Erros));
        Assert.Empty(_banco.Gravadas);
        Assert.Equal(10m, _pf.Carteira.Saldo);
        Assert.Equal(1, _banco.Desfeitas);
        AssertRecusa("saldo-insuficiente");
    }

    [Fact]
    public async Task Executar_CarteiraSumiuAntesDoBloqueio_404EDesfaz()
    {
        _banco.OmitirCarteirasNoBloqueio = true;

        var resultado = await Transferir("12ABC34501DE35", 1m);

        Assert.Equal(ErrosTransferencia.DestinatarioNaoEncontrado, Assert.Single(resultado.Erros));
        Assert.Equal(1, _banco.Desfeitas);
    }

    [Fact]
    public async Task Replay_MesmaChaveEMesmosDados_DevolveOriginalSemDebitar()
    {
        var chave = Guid.NewGuid();
        var primeira = await Transferir("12ABC34501DE35", 4m, chave: chave);

        var segunda = await Transferir("12.ABC.345/01DE-35", 4m, "outra descrição", chave);

        Assert.True(segunda.Valor.Replay);
        Assert.Equal(primeira.Valor.Comprovante.TransferenciaId, segunda.Valor.Comprovante.TransferenciaId);
        Assert.Equal("•••.982.247-••", segunda.Valor.Comprovante.Pagador.Documento);
        Assert.Equal(chave, segunda.Valor.Comprovante.ChaveIdempotencia);
        Assert.Equal(6m, _pf.Carteira.Saldo);
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.replays");
    }

    [Theory]
    [InlineData("12ABC34501DE35", 5)]
    [InlineData("11222333000181", 4)]
    public async Task Replay_MesmaChaveComDadosDiferentes_409(string documento, decimal valor)
    {
        _banco.Criar("Outra Empresa", "11222333000181", TipoConta.PessoaJuridica, 0m);
        var chave = Guid.NewGuid();
        await Transferir("12ABC34501DE35", 4m, chave: chave);

        var resultado = await Transferir(documento, valor, chave: chave);

        Assert.Equal(ErrosTransferencia.IdempotenciaDivergente, Assert.Single(resultado.Erros));
    }

    [Fact]
    public async Task ChaveGravadaPorOutraRequisicaoNaConfirmacao_DevolveReplay()
    {
        var chave = Guid.NewGuid();
        var outra = Transferencia.Executar(_pf.Carteira, _pj.Carteira, ValorTransferencia.Criar(2m).Valor, null, chave, DateTimeOffset.UtcNow, Nomes).Valor;
        _banco.ForcarChaveDuplicada = true;
        _banco.AntesDeConfirmar = () => _banco.Gravadas.Add(outra);

        var resultado = await Transferir("12ABC34501DE35", 2m, chave: chave);

        Assert.True(resultado.Valor.Replay);
        Assert.Equal(outra.Transferencia.Id, resultado.Valor.Comprovante.TransferenciaId);
    }

    // ---------- Limite por operação (specs/009-limite-transferencia, US1) ----------

    [Fact]
    public async Task LimitePorOperacao_ValorIgual_Conclui()
    {
        _limites.PessoaFisica.PorOperacao = 4m;

        Assert.True((await Transferir("12ABC34501DE35", 4m)).Sucesso);
    }

    [Fact]
    public async Task LimitePorOperacao_Acima_RecusaAntesDoBloqueio()
    {
        _limites.PessoaFisica.PorOperacao = 4m;

        var resultado = await Transferir("12ABC34501DE35", 4.01m);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal("limite-excedido", erro.Codigo);
        Assert.Equal("PorOperacao", erro.Detalhes!["tipoLimite"]);
        Assert.Equal(4m, erro.Detalhes["valorDisponivel"]);
        Assert.Equal(0, _banco.TransacoesIniciadas);
        Assert.Empty(_banco.Gravadas);
        Assert.Equal(10m, _pf.Carteira.Saldo);
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.recusadas"
            && m.Tags["motivo"] == "limite-excedido" && m.Tags["tipo_limite"] == "PorOperacao");
    }

    [Fact]
    public async Task LimitePorOperacao_AcimaDoLimiteEDoSaldo_RecusaPorLimite()
    {
        _limites.PessoaFisica.PorOperacao = 4m;

        var resultado = await Transferir("12ABC34501DE35", 50m);

        Assert.Equal("limite-excedido", Assert.Single(resultado.Erros).Codigo);
    }

    [Fact]
    public async Task LimitePorOperacao_Recusa_GeraLogDeAuditoriaSemDadosPessoais()
    {
        _limites.PessoaFisica.PorOperacao = 4m;

        await Transferir("12ABC34501DE35", 5m);

        var registro = Assert.Single(_logger.Registros, r => r.Nivel == LogLevel.Warning);
        Assert.Equal(("PorOperacao", _pf.Carteira.Id.Valor, 5m, 4m, 4m), (
            registro.Propriedades["TipoLimite"]?.ToString(),
            (Guid)registro.Propriedades["CarteiraOrigemId"]!,
            (decimal)registro.Propriedades["Valor"]!,
            (decimal)registro.Propriedades["Limite"]!,
            (decimal)registro.Propriedades["ValorDisponivel"]!));
        Assert.DoesNotContain("52998224725", registro.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("Lucas", registro.Mensagem, StringComparison.Ordinal);
    }

    // ---------- Limite diário (specs/009-limite-transferencia, US2) ----------

    [Fact]
    public async Task LimiteDiario_SomaAcima_RecusaDentroDoBloqueioEDesfaz()
    {
        _limites.PessoaFisica.Diario = 8m;
        _banco.TransferidoAntes = 5m;

        var resultado = await Transferir("12ABC34501DE35", 3.01m);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(("limite-excedido", "Diario", 8m, 3m), (erro.Codigo, erro.Detalhes!["tipoLimite"], erro.Detalhes["limite"], erro.Detalhes["valorDisponivel"]));
        Assert.Equal(["Bloquear", "Somar"], _banco.Chamadas);
        Assert.Equal(1, _banco.Desfeitas);
        Assert.Empty(_banco.Gravadas);
        Assert.Equal(10m, _pf.Carteira.Saldo);
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.recusadas" && m.Tags["tipo_limite"] == "Diario");
        Assert.Single(_logger.Registros, r => r.Nivel == LogLevel.Warning);
    }

    [Fact]
    public async Task LimiteDiario_SomaIgual_Conclui()
    {
        _limites.PessoaFisica.Diario = 8m;
        _banco.TransferidoAntes = 5m;

        Assert.True((await Transferir("12ABC34501DE35", 3m)).Sucesso);
    }

    [Fact]
    public async Task LimiteDiario_SomaUsaODiaDeBrasilia()
    {
        _limites.PessoaFisica.Diario = 8m;

        await Transferir("12ABC34501DE35", 1m);

        Assert.Equal(
            (new DateTimeOffset(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 18, 3, 0, 0, TimeSpan.Zero)),
            _banco.UltimoIntervalo);
    }

    [Fact]
    public async Task LimiteDiario_CabeNoLimiteMasNaoNoSaldo_SaldoInsuficiente()
    {
        _limites.PessoaFisica.Diario = 100m;

        var resultado = await Transferir("12ABC34501DE35", 10.01m);

        Assert.Equal(ErrosTransferencia.SaldoInsuficiente, Assert.Single(resultado.Erros));
    }

    // ---------- Replay e limites (specs/009-limite-transferencia, US3) ----------

    [Fact]
    public async Task Replay_AntesDoBloqueio_NaoVerificaLimites()
    {
        var chave = Guid.NewGuid();
        await Transferir("12ABC34501DE35", 4m, chave: chave);
        _limites.PessoaFisica.PorOperacao = 1m;
        _limites.PessoaFisica.Diario = 1m;
        _banco.Chamadas.Clear();

        var replay = await Transferir("12ABC34501DE35", 4m, chave: chave);

        Assert.True(replay.Valor.Replay);
        Assert.Empty(_banco.Chamadas);
        Assert.DoesNotContain(_metricas.Medicoes, m => m.Nome == "bank.transferencias.recusadas");
    }

    [Fact]
    public async Task Replay_DentroDoBloqueio_NaoSomaODia()
    {
        var chave = Guid.NewGuid();
        var outra = Transferencia.Executar(_pf.Carteira, _pj.Carteira, ValorTransferencia.Criar(2m).Valor, null, chave, Relogio.GetUtcNow(), Nomes).Valor;
        _limites.PessoaFisica.Diario = 1m;
        _banco.AntesDeBloquear = () => _banco.Gravadas.Add(outra);

        var resultado = await Transferir("12ABC34501DE35", 2m, chave: chave);

        Assert.True(resultado.Valor.Replay);
        Assert.Equal(["Bloquear"], _banco.Chamadas);
        Assert.DoesNotContain(_metricas.Medicoes, m => m.Nome == "bank.transferencias.recusadas");
    }

    private Task<Resultado<TransferenciaRealizada>> Transferir(string documento, decimal valor, string? descricao = null, Guid? chave = null) =>
        _casoDeUso.ExecutarAsync(new RealizarTransferenciaComando(_pf.Id, (chave ?? Guid.NewGuid()).ToString(), documento, valor, descricao), CancellationToken.None);

    private void AssertRecusa(string motivo) =>
        Assert.Contains(_metricas.Medicoes, m => m.Nome == "bank.transferencias.recusadas" && m.Tags["motivo"] == motivo);
}

/// <summary>Captura as medições de <see cref="MetricasTransferencias"/>.</summary>
internal sealed class MetricasCapturadasTransferencias : IDisposable
{
    private readonly MetricasCapturadasBase _base = new(f => new MetricasTransferencias(f));

    public MetricasTransferencias Metricas => (MetricasTransferencias)_base.Instancia;

    public IReadOnlyCollection<(string Nome, double Valor, Dictionary<string, string?> Tags)> Medicoes => _base.Medicoes;

    public void Dispose() => _base.Dispose();
}
