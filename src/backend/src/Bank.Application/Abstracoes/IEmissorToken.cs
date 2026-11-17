using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Token de acesso emitido no login.</summary>
public sealed record TokenEmitido(string Token, int ExpiraEmSegundos);

/// <summary>Emissão do token de acesso (JWT — research R4).</summary>
public interface IEmissorToken
{
    TokenEmitido Emitir(Usuario usuario);
}
