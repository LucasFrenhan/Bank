using System.Globalization;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Domain;

/// <summary>
/// Fábricas e texto da notificação de auditoria (specs/011-notificacoes-auditoria, FR-001–FR-004;
/// contracts/notificacoes-auditoria.md; research R1, R5).
/// </summary>
public class NotificacaoAuditoriaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly EventoId Evento = EventoId.De(Guid.CreateVersion7());
    private static readonly UsuarioId Usuario = UsuarioId.De(Guid.CreateVersion7());

    [Fact]
    public void ComprovanteEnvio_ComNome_DestinadoAoPagador()
    {
        var notificacao = NotificacaoAuditoria.ComprovanteEnvio(Evento, Usuario, 150m, "Maria Souza", Agora);

        Assert.Equal(TipoNotificacao.ComprovanteEnvio, notificacao.Tipo);
        Assert.Equal(CanalNotificacao.Simulado, notificacao.Canal);
        Assert.Equal(Usuario, notificacao.UsuarioDestinoId);
        Assert.Equal(Evento, notificacao.EventoId);
        Assert.Equal("Você enviou R$ 150,00 para Maria Souza.", notificacao.Mensagem);
        Assert.NotEqual(Guid.Empty, notificacao.Id.Valor);
        Assert.Equal(Agora, notificacao.CriadoEm);
    }

    [Fact]
    public void AvisoCredito_ComNome_DestinadoAoRecebedor()
    {
        var notificacao = NotificacaoAuditoria.AvisoCredito(Evento, Usuario, 150m, "João Silva", Agora);

        Assert.Equal(TipoNotificacao.AvisoCredito, notificacao.Tipo);
        Assert.Equal(CanalNotificacao.Simulado, notificacao.Canal);
        Assert.Equal(Usuario, notificacao.UsuarioDestinoId);
        Assert.Equal("Você recebeu R$ 150,00 de João Silva.", notificacao.Mensagem);
        Assert.Equal(Agora, notificacao.CriadoEm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemNome_TextoSoComOValor(string? nome)
    {
        Assert.Equal("Você enviou R$ 150,00.", NotificacaoAuditoria.ComprovanteEnvio(Evento, Usuario, 150m, nome, Agora).Mensagem);
        Assert.Equal("Você recebeu R$ 150,00.", NotificacaoAuditoria.AvisoCredito(Evento, Usuario, 150m, nome, Agora).Mensagem);
    }

    [Theory]
    [InlineData("0.01", "0,01")]
    [InlineData("1234.5", "1.234,50")]
    [InlineData("1234567.89", "1.234.567,89")]
    public void Valor_NoFormatoBrasileiro(string valor, string esperado)
    {
        var notificacao = NotificacaoAuditoria.ComprovanteEnvio(Evento, Usuario, decimal.Parse(valor, CultureInfo.InvariantCulture), null, Agora);

        Assert.Equal($"Você enviou R$ {esperado}.", notificacao.Mensagem);
    }

    [Fact]
    public void Nome_SemEspacosNasExtremidades()
    {
        var notificacao = NotificacaoAuditoria.AvisoCredito(Evento, Usuario, 1m, "  Maria Souza  ", Agora);

        Assert.Equal("Você recebeu R$ 1,00 de Maria Souza.", notificacao.Mensagem);
    }

    [Fact]
    public void Formato_NaoDependeDaCulturaDoProcesso()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var notificacao = NotificacaoAuditoria.ComprovanteEnvio(Evento, Usuario, 1234.5m, null, Agora);

            Assert.Equal("Você enviou R$ 1.234,50.", notificacao.Mensagem);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void CadaNotificacao_TemIdentificadorProprio()
    {
        var comprovante = NotificacaoAuditoria.ComprovanteEnvio(Evento, Usuario, 1m, null, Agora);
        var aviso = NotificacaoAuditoria.AvisoCredito(Evento, Usuario, 1m, null, Agora);

        Assert.NotEqual(comprovante.Id, aviso.Id);
    }
}
