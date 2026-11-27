using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Sessoes;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Autenticacao;

/// <summary>
/// "Sair" (specs/016-sessao-refresh-token, US2; FR-011): revoga a sessão dona da credencial, ativa ou já trocada
/// (corrida com uma renovação), com o motivo <see cref="MotivoRevogacao.Logout"/>. Idempotente: sem credencial,
/// com uma desconhecida ou de sessão já revogada, nada acontece.
/// </summary>
public sealed partial class EncerrarSessao(
    ISessaoRepositorio sessoes,
    IGeradorCredencial gerador,
    IUnidadeDeTrabalho unidade,
    TimeProvider relogio,
    MetricasUsuarios metricas,
    ILogger<EncerrarSessao> logger)
{
    public async Task ExecutarAsync(string? credencial, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(credencial))
        {
            return;
        }

        await unidade.IniciarAsync(cancellationToken);
        var sessao = await sessoes.ObterPorCredencialComBloqueioAsync(gerador.CalcularHash(credencial), cancellationToken);
        if (sessao is null || !sessao.Revogar(MotivoRevogacao.Logout, relogio.GetUtcNow()))
        {
            await unidade.DesfazerAsync(cancellationToken);
            return;
        }

        await unidade.ConfirmarAsync(cancellationToken);
        metricas.RegistrarRevogacao(MotivoRevogacao.Logout);
        LogEncerrada(logger, sessao.Id.Valor);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sessão {SessaoId} encerrada pela pessoa")]
    private static partial void LogEncerrada(ILogger logger, Guid sessaoId);
}
