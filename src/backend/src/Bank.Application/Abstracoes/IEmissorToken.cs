using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Token de acesso emitido no login ou na renovação.</summary>
public sealed record TokenEmitido(string Token, int ExpiraEmSegundos);

/// <summary>Emissão do token de acesso (JWT — research R4; claim <c>sid</c> da specs/016-sessao-refresh-token, R5).</summary>
public interface IEmissorToken
{
    TokenEmitido Emitir(Usuario usuario, SessaoId sessaoId);
}
