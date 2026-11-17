using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>Regras de débito, crédito e transferência (spec 006: RN-03, RN-05, RN-06; FR-006, FR-015).</summary>
public class TransferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Debitar_ComSaldoSuficiente_SubtraiEAtualiza_InclusiveAteZero()
    {
        var carteira = NovaCarteira(10m);

        Assert.True(carteira.Debitar(10m, Agora.AddMinutes(1)).Sucesso);

        Assert.Equal(0m, carteira.Saldo);
        Assert.Equal(Agora.AddMinutes(1), carteira.AtualizadoEm);
    }

    [Fact]
    public void Debitar_SemSaldo_RecusaSemAlterar()
    {
        var carteira = NovaCarteira(10m);

        var resultado = carteira.Debitar(10.01m, Agora.AddMinutes(1));

        Assert.Equal(ErrosTransferencia.SaldoInsuficiente, Assert.Single(resultado.Erros));
        Assert.Equal(10m, carteira.Saldo);
        Assert.Equal(Agora, carteira.AtualizadoEm);
    }

    [Fact]
    public void Creditar_SomaEAtualiza()
    {
        var carteira = NovaCarteira(0m);

        carteira.Creditar(4.5m, Agora.AddMinutes(1));

        Assert.Equal("4.50", carteira.Saldo.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Agora.AddMinutes(1), carteira.AtualizadoEm);
    }

    [Fact]
    public void Executar_GeraTransferenciaLancamentosEEvento()
    {
        var origem = NovaCarteira(10m);
        var destino = NovaCarteira(0m, "12ABC34501DE35", TipoConta.PessoaJuridica);
        var chave = Guid.NewGuid();

        var execucao = Transferencia.Executar(origem, destino, ValorTransferencia.Criar(4.5m).Valor, "Almoço", chave, Agora).Valor;

        Assert.Equal(5.5m, origem.Saldo);
        Assert.Equal(4.5m, destino.Saldo);

        var transferencia = execucao.Transferencia;
        Assert.Equal(StatusTransferencia.Concluida, transferencia.Status);
        Assert.Equal(chave, transferencia.ChaveIdempotencia);
        Assert.Equal((origem.Id, destino.Id, 4.5m, "Almoço", Agora), (transferencia.CarteiraOrigemId, transferencia.CarteiraDestinoId, transferencia.Valor, transferencia.Descricao, transferencia.CriadoEm));

        Assert.Equal((origem.Id, DirecaoLancamento.Saida, TipoLancamento.Transferencia, transferencia.Id), (execucao.Saida.CarteiraId, execucao.Saida.Direcao, execucao.Saida.Tipo, execucao.Saida.TransferenciaId));
        Assert.Equal((destino.Id, DirecaoLancamento.Entrada, transferencia.Id), (execucao.Entrada.CarteiraId, execucao.Entrada.Direcao, execucao.Entrada.TransferenciaId));
        Assert.All([execucao.Saida, execucao.Entrada], l => Assert.Equal((4.5m, "Almoço", Agora), (l.Valor, l.Descricao, l.CriadoEm)));

        var evento = execucao.Evento;
        Assert.Equal((transferencia.Id, origem.UsuarioId, destino.UsuarioId, 4.5m, Agora), (evento.TransferenciaId, evento.PagadorUsuarioId, evento.RecebedorUsuarioId, evento.Valor, evento.OcorridoEm));
        Assert.Equal(7, evento.EventoId.Version);
    }

    [Fact]
    public void Executar_ParaAPropriaCarteira_Autotransferencia()
    {
        var carteira = NovaCarteira(10m);

        var resultado = Transferencia.Executar(carteira, carteira, ValorTransferencia.Criar(1m).Valor, null, Guid.NewGuid(), Agora);

        Assert.Equal(ErrosTransferencia.Autotransferencia, Assert.Single(resultado.Erros));
        Assert.Equal(10m, carteira.Saldo);
    }

    [Fact]
    public void Executar_SemSaldo_RecusaSemCreditarODestino()
    {
        var origem = NovaCarteira(10m);
        var destino = NovaCarteira(0m, "12ABC34501DE35", TipoConta.PessoaJuridica);

        var resultado = Transferencia.Executar(origem, destino, ValorTransferencia.Criar(300m).Valor, null, Guid.NewGuid(), Agora);

        Assert.Equal(ErrosTransferencia.SaldoInsuficiente, Assert.Single(resultado.Erros));
        Assert.Equal((10m, 0m), (origem.Saldo, destino.Saldo));
    }

    private static Carteira NovaCarteira(decimal saldo, string documento = "52998224725", TipoConta tipo = TipoConta.PessoaFisica) =>
        Usuario.Criar("Titular", Email.Criar($"{Guid.NewGuid():N}@exemplo.com.br").Valor, Documento.Criar(documento, tipo).Valor, "hash", saldo, Agora).Carteira;
}
