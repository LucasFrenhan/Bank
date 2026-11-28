using Bank.Application.Abstracoes;
using Bank.Application.Transferencias;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bank.UnitTests.Application;

/// <summary>Consulta de transferência por identificador (specs/008-consulta-transferencia: US1–US3).</summary>
public sealed class ConsultarTransferenciaTests : IDisposable
{
    private readonly BancoFalso _banco = new();
    private readonly MetricasCapturadasTransferencias _metricas = new();
    private readonly ConsultarTransferencia _casoDeUso;
    private readonly Usuario _pagador;
    private readonly Usuario _recebedor;
    private readonly Usuario _terceiro;
    private readonly Guid _chave = Guid.NewGuid();
    private TransferenciaId _transferenciaId;

    public ConsultarTransferenciaTests()
    {
        _casoDeUso = new ConsultarTransferencia(_banco, _metricas.Metricas);
        _pagador = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
        _recebedor = _banco.Criar("Cafeteria Ltda", "12ABC34501DE35", TipoConta.PessoaJuridica, 0m);
        _terceiro = _banco.Criar("Mariana Costa", "11144477735", TipoConta.PessoaFisica, 10m);
    }

    public void Dispose() => _metricas.Dispose();

    private async Task TransferirAsync()
    {
        var transferir = new RealizarTransferencia(_banco, _banco, _banco, _banco, new MonitorDeOpcoes<LimitesTransferenciaOptions>(new()), new CalendarioBrasilia(), TimeProvider.System, _metricas.Metricas, NullLogger<RealizarTransferencia>.Instance);
        var resultado = await transferir.ExecutarAsync(
            new RealizarTransferenciaComando(_pagador.Id, _chave.ToString(), "12ABC34501DE35", 4.5m, "Café"),
            CancellationToken.None);
        _transferenciaId = resultado.Valor.Comprovante.TransferenciaId;
    }

    private Task<Bank.SharedKernel.Resultado<Comprovante>> Consultar(UsuarioId usuarioId, string? id) =>
        _casoDeUso.ExecutarAsync(new ConsultarTransferenciaConsulta(usuarioId, id), CancellationToken.None);

    private int Consultas(string resultado) =>
        _metricas.Medicoes.Count(m => m.Nome == "bank.transferencias.consultas" && m.Tags["resultado"] == resultado);

    // ---------- US1: pagador ----------

    [Fact]
    public async Task Pagador_RecebeOComprovanteComChaveEDocumentoMascarado()
    {
        await TransferirAsync();

        var resultado = await Consultar(_pagador.Id, _transferenciaId.ToString());

        Assert.True(resultado.Sucesso);
        var comprovante = resultado.Valor;
        Assert.Equal((_transferenciaId, (Guid?)_chave), (comprovante.TransferenciaId, comprovante.ChaveIdempotencia));
        Assert.Equal((_pagador.Id, "Lucas Silva", "•••.982.247-••"), (comprovante.Pagador.UsuarioId, comprovante.Pagador.Nome, comprovante.Pagador.Documento));
        Assert.Equal((_recebedor.Id, "12ABC34501DE35", TipoConta.PessoaJuridica), (comprovante.Recebedor.UsuarioId, comprovante.Recebedor.Documento, comprovante.Recebedor.TipoConta));
        Assert.Equal((4.5m, "Café", StatusTransferencia.Concluida), (comprovante.Valor, comprovante.Descricao, comprovante.Status));
        Assert.Equal(1, Consultas("encontrada"));
    }

    // ---------- US2: recebedor ----------

    [Fact]
    public async Task Recebedor_RecebeOMesmoComprovanteSemAChave()
    {
        await TransferirAsync();
        var doPagador = (await Consultar(_pagador.Id, _transferenciaId.ToString())).Valor;

        var resultado = await Consultar(_recebedor.Id, _transferenciaId.ToString());

        Assert.True(resultado.Sucesso);
        Assert.Null(resultado.Valor.ChaveIdempotencia);
        Assert.Equal(doPagador with { ChaveIdempotencia = null }, resultado.Valor);
        Assert.Equal("•••.982.247-••", resultado.Valor.Pagador.Documento);
        Assert.Equal(2, Consultas("encontrada"));
    }

    // ---------- US3: terceiros, inexistentes e inválidos ----------

    [Fact]
    public async Task TerceiroEInexistente_MesmoErroEMetricaNaoEncontrada()
    {
        await TransferirAsync();

        var deTerceiro = await Consultar(_terceiro.Id, _transferenciaId.ToString());
        var inexistente = await Consultar(_pagador.Id, Guid.NewGuid().ToString());

        Assert.Equal(ErrosTransferencia.TransferenciaNaoEncontrada, Assert.Single(deTerceiro.Erros));
        Assert.Equal(ErrosTransferencia.TransferenciaNaoEncontrada, Assert.Single(inexistente.Erros));
        Assert.Equal(2, Consultas("nao-encontrada"));
        Assert.Equal(0, Consultas("encontrada"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("019a8f55-2222-7890-abcd")]
    public async Task IdentificadorInvalido_ErroNoCampoSemConsultarORepositorio(string? id)
    {
        var resultado = await Consultar(_pagador.Id, id);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal(("validacao", ConsultarTransferencia.CampoIdentificador), (erro.Codigo, erro.Campo));
        Assert.Equal(0, _banco.LeiturasPorId);
        Assert.Equal(1, Consultas("invalida"));
    }

    [Fact]
    public async Task IdentificadorEmMaiusculasOuSemHifens_EAceito()
    {
        await TransferirAsync();

        var maiusculas = await Consultar(_pagador.Id, _transferenciaId.ToString().ToUpperInvariant());
        var semHifens = await Consultar(_pagador.Id, _transferenciaId.Valor.ToString("N"));

        Assert.True(maiusculas.Sucesso);
        Assert.True(semHifens.Sucesso);
    }

    [Fact]
    public async Task Consulta_NaoAlteraSaldosNemGravaNada()
    {
        await TransferirAsync();
        var gravadas = _banco.Gravadas.Count;

        for (var i = 0; i < 3; i++)
        {
            await Consultar(_pagador.Id, _transferenciaId.ToString());
            await Consultar(_terceiro.Id, _transferenciaId.ToString());
        }

        Assert.Equal((5.5m, 4.5m, 10m), (_pagador.Carteira.Saldo, _recebedor.Carteira.Saldo, _terceiro.Carteira.Saldo));
        Assert.Equal(gravadas, _banco.Gravadas.Count);
    }
}
