using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Sessoes;
using Bank.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Autenticacao;

/// <summary>
/// Renova a sessão (specs/016-sessao-refresh-token, US1, US3, US4; FR-006 a FR-010): numa transação, bloqueia a
/// sessão dona da credencial, aplica <see cref="Sessao.Renovar"/> e emite token e credencial novos. Toda recusa,
/// inclusive o reuso, devolve o mesmo erro; o motivo fica só no log e na métrica.
/// </summary>
public sealed partial class RenovarSessao(
    ISessaoRepositorio sessoes,
    IUsuarioRepositorio usuarios,
    IGeradorCredencial gerador,
    IEmissorToken emissorToken,
    IUnidadeDeTrabalho unidade,
    IOptionsMonitor<SessaoOptions> opcoes,
    TimeProvider relogio,
    MetricasUsuarios metricas,
    ILogger<RenovarSessao> logger)
{
    public const string ResultadoSucesso = "sucesso";
    public const string ResultadoRecusada = "recusada";
    public const string ResultadoReuso = "reuso";

    public async Task<Resultado<SessaoEmitida>> ExecutarAsync(string? credencialApresentada, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(credencialApresentada))
        {
            return Recusar(null, "sem-credencial");
        }

        var agora = relogio.GetUtcNow();
        var limites = opcoes.CurrentValue.Limites();
        var apresentada = gerador.CalcularHash(credencialApresentada);

        await unidade.IniciarAsync(cancellationToken);
        var sessao = await sessoes.ObterPorCredencialComBloqueioAsync(apresentada, cancellationToken);
        if (sessao is null)
        {
            await unidade.DesfazerAsync(cancellationToken);
            return Recusar(null, "desconhecida");
        }

        var nova = gerador.Gerar();
        switch (sessao.Renovar(apresentada, nova.Hash, agora, limites))
        {
            case ResultadoRenovacao.Recusada recusada:
                await unidade.DesfazerAsync(cancellationToken);
                return Recusar(sessao.Id, recusada.Motivo.ToString());

            case ResultadoRenovacao.ReusoDetectado:
                await unidade.ConfirmarAsync(cancellationToken); // grava a revogação antes de responder
                metricas.RegistrarRenovacao(ResultadoReuso);
                metricas.RegistrarRevogacao(MotivoRevogacao.Reuso);
                LogReuso(logger, sessao.Id.Valor, sessao.UsuarioId.Valor);
                return Resultado.Falha<SessaoEmitida>(ErrosSessao.NaoAutenticado);
        }

        var usuario = await usuarios.ObterPorIdAsync(sessao.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            await unidade.DesfazerAsync(cancellationToken);
            return Recusar(sessao.Id, "usuario-inexistente");
        }

        await unidade.ConfirmarAsync(cancellationToken);
        metricas.RegistrarRenovacao(ResultadoSucesso);
        LogRenovada(logger, sessao.Id.Valor);

        return Resultado.Ok(EmissaoSessao.Emitir(emissorToken, usuario, nova, sessao, agora, limites));
    }

    private Resultado<SessaoEmitida> Recusar(SessaoId? sessaoId, string motivo)
    {
        metricas.RegistrarRenovacao(ResultadoRecusada);
        LogRecusada(logger, motivo, sessaoId?.Valor);
        return Resultado.Falha<SessaoEmitida>(ErrosSessao.NaoAutenticado);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sessão {SessaoId} renovada")]
    private static partial void LogRenovada(ILogger logger, Guid sessaoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renovação recusada ({Motivo}); sessão {SessaoId}")]
    private static partial void LogRecusada(ILogger logger, string motivo, Guid? sessaoId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reuso de credencial detectado; sessão {SessaoId} do usuário {UsuarioId} revogada")]
    private static partial void LogReuso(ILogger logger, Guid sessaoId, Guid usuarioId);
}
