using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Autenticacao;

public sealed record AutenticarComando(string? Email, string? Senha);

public sealed record TokenAcesso(string Token, string TipoToken, int ExpiraEmSegundos)
{
    public const string TipoBearer = "Bearer";
}

/// <summary>
/// Hash de uma senha fictícia, verificado quando o e-mail não existe para que o tempo de
/// resposta não revele e-mails cadastrados (FR-019; research R3). Gerado uma vez por processo.
/// </summary>
public sealed class HashSenhaFicticio(IHasherSenha hasherSenha)
{
    private readonly Lazy<string> _hash = new(() => hasherSenha.GerarHash(Guid.NewGuid().ToString("N") + "Aa1!"));

    public string Valor => _hash.Value;
}

/// <summary>Caso de uso de login (US2): valida credenciais e emite o token de acesso.</summary>
public sealed partial class Autenticar(
    IUsuarioRepositorio repositorio,
    IHasherSenha hasherSenha,
    HashSenhaFicticio hashFicticio,
    IEmissorToken emissorToken,
    MetricasUsuarios metricas,
    ILogger<Autenticar> logger)
{
    public async Task<Resultado<TokenAcesso>> ExecutarAsync(AutenticarComando comando, CancellationToken cancellationToken)
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
            return Resultado.Falha<TokenAcesso>(erros);
        }

        var usuario = await repositorio.ObterPorEmailAsync(Email.Normalizar(comando.Email), cancellationToken);

        // Sempre verifica um hash, mesmo sem usuário, para igualar o tempo de resposta.
        var verificacao = hasherSenha.Verificar(usuario?.SenhaHash ?? hashFicticio.Valor, comando.Senha!);

        if (usuario is null || verificacao == ResultadoVerificacaoSenha.Falha)
        {
            metricas.RegistrarLogin(sucesso: false);
            LogLoginRecusado(logger);
            return Resultado.Falha<TokenAcesso>(ErrosUsuario.CredenciaisInvalidas);
        }

        if (verificacao == ResultadoVerificacaoSenha.SucessoRehashNecessario)
        {
            usuario.AtualizarSenhaHash(hasherSenha.GerarHash(comando.Senha!));
            await repositorio.AtualizarAsync(usuario, cancellationToken);
        }

        var token = emissorToken.Emitir(usuario);
        metricas.RegistrarLogin(sucesso: true);
        LogLoginConcluido(logger, usuario.Id);

        return Resultado.Ok(new TokenAcesso(token.Token, TokenAcesso.TipoBearer, token.ExpiraEmSegundos));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login concluído para o usuário {UsuarioId}")]
    private static partial void LogLoginConcluido(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login recusado: credenciais inválidas")]
    private static partial void LogLoginRecusado(ILogger logger);
}
