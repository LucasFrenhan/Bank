using Bank.Application.Abstracoes;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;

namespace Bank.Application.Autenticacao;

/// <summary>
/// Resultado do login e da renovação (specs/016-sessao-refresh-token; contracts/autenticacao-sessao-api.md): token de
/// acesso, a credencial nova (só para o cookie) e os prazos da sessão em segundos.
/// </summary>
public sealed record SessaoEmitida(
    string Token,
    string TipoToken,
    int ExpiraEmSegundos,
    string Credencial,
    int CredencialExpiraEmSegundos,
    int SessaoExpiraPorInatividadeEmSegundos,
    int SessaoExpiraEmSegundos)
{
    public const string TipoBearer = "Bearer";

    internal static SessaoEmitida De(TokenEmitido token, CredencialGerada credencial, Sessao sessao, DateTimeOffset agora, LimitesSessao limites)
    {
        var prazoInatividade = sessao.PrazoInatividade(agora, limites);
        return new SessaoEmitida(
            token.Token,
            TipoBearer,
            token.ExpiraEmSegundos,
            credencial.Valor,
            prazoInatividade,
            prazoInatividade,
            sessao.PrazoAbsoluto(agora));
    }
}

/// <summary>Emite o token de acesso para a sessão.</summary>
internal static class EmissaoSessao
{
    public static SessaoEmitida Emitir(
        IEmissorToken emissor, Usuario usuario, CredencialGerada credencial, Sessao sessao, DateTimeOffset agora, LimitesSessao limites) =>
        SessaoEmitida.De(emissor.Emitir(usuario, sessao.Id), credencial, sessao, agora, limites);
}
