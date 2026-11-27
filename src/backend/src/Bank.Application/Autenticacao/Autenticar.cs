using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Autenticacao;

/// <param name="CredencialAtual">Credencial de renovação enviada pelo navegador, se houver (specs/016, FR-012).</param>
public sealed record AutenticarComando(string? Email, string? Senha, string? CredencialAtual = null);

/// <summary>
/// Hash de uma senha fictícia, verificado quando o e-mail não existe para que o tempo de
/// resposta não revele e-mails cadastrados (FR-019; research R3). Gerado uma vez por processo.
/// </summary>
public sealed class HashSenhaFicticio(IHasherSenha hasherSenha)
{
    private readonly Lazy<string> _hash = new(() => hasherSenha.GerarHash(Guid.NewGuid().ToString("N") + "Aa1!"));

    public string Valor => _hash.Value;
}

/// <summary>
/// Caso de uso de login (US2 da spec 003): valida credenciais, inicia a sessão no servidor e emite o token de
/// acesso e a credencial de renovação (specs/016-sessao-refresh-token, FR-001, FR-012). A sessão apresentada pelo
/// navegador, se houver, é revogada na mesma transação.
/// </summary>
public sealed partial class Autenticar(
    IUsuarioRepositorio repositorio,
    IHasherSenha hasherSenha,
    HashSenhaFicticio hashFicticio,
    IEmissorToken emissorToken,
    ISessaoRepositorio sessoes,
    IGeradorCredencial gerador,
    IUnidadeDeTrabalho unidade,
    IOptionsMonitor<SessaoOptions> opcoes,
    TimeProvider relogio,
    MetricasUsuarios metricas,
    ILogger<Autenticar> logger)
{
    public async Task<Resultado<SessaoEmitida>> ExecutarAsync(AutenticarComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var erros = new List<Erro>();
        if (string.IsNullOrWhiteSpace(comando.Email))
        {
            erros.Add(Erro.Validacao(Email.Campo, "O e-mail é obrigatório."));
        }

        if (string.IsNullOrEmpty(comando.Senha))
        {
            erros.Add(Erro.Validacao(PoliticaSenha.Campo, "A senha é obrigatória."));
        }

        if (erros.Count > 0)
        {
            return Resultado.Falha<SessaoEmitida>(erros);
        }

        var usuario = await repositorio.ObterPorEmailAsync(Email.Normalizar(comando.Email), cancellationToken);

        // Sempre verifica um hash, mesmo sem usuário, para igualar o tempo de resposta.
        var verificacao = hasherSenha.Verificar(usuario?.SenhaHash ?? hashFicticio.Valor, comando.Senha!);

        if (usuario is null || verificacao == ResultadoVerificacaoSenha.Falha)
        {
            metricas.RegistrarLogin(sucesso: false);
            LogLoginRecusado(logger);
            return Resultado.Falha<SessaoEmitida>(ErrosUsuario.CredenciaisInvalidas);
        }

        if (verificacao == ResultadoVerificacaoSenha.SucessoRehashNecessario)
        {
            usuario.AtualizarSenhaHash(hasherSenha.GerarHash(comando.Senha!));
            await repositorio.AtualizarAsync(usuario, cancellationToken);
        }

        var agora = relogio.GetUtcNow();
        var limites = opcoes.CurrentValue.Limites();
        var credencial = gerador.Gerar();
        var sessao = Sessao.Iniciar(usuario.Id, credencial.Hash, agora, limites);

        await unidade.IniciarAsync(cancellationToken);
        var anterior = await RevogarAnteriorAsync(comando.CredencialAtual, agora, cancellationToken);
        sessoes.Adicionar(sessao);
        await unidade.ConfirmarAsync(cancellationToken);

        if (anterior is not null)
        {
            metricas.RegistrarRevogacao(MotivoRevogacao.NovoLogin);
            LogSessaoAnteriorRevogada(logger, anterior.Value.Valor);
        }

        metricas.RegistrarLogin(sucesso: true);
        LogLoginConcluido(logger, usuario.Id.Valor, sessao.Id.Valor);

        return Resultado.Ok(EmissaoSessao.Emitir(emissorToken, usuario, credencial, sessao, agora, limites));
    }

    /// <summary>Revoga a sessão apresentada pelo navegador (FR-012); devolve o id se revogou nesta chamada.</summary>
    private async Task<SessaoId?> RevogarAnteriorAsync(string? credencialAtual, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(credencialAtual))
        {
            return null;
        }

        var anterior = await sessoes.ObterPorCredencialComBloqueioAsync(gerador.CalcularHash(credencialAtual), cancellationToken);
        return anterior is not null && anterior.Revogar(MotivoRevogacao.NovoLogin, agora) ? anterior.Id : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login concluído para o usuário {UsuarioId} na sessão {SessaoId}")]
    private static partial void LogLoginConcluido(ILogger logger, Guid usuarioId, Guid sessaoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login recusado: credenciais inválidas")]
    private static partial void LogLoginRecusado(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sessão {SessaoId} revogada por novo login no mesmo navegador")]
    private static partial void LogSessaoAnteriorRevogada(ILogger logger, Guid sessaoId);
}
