using Bank.Application.Abstracoes;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bank.Infrastructure.Seguranca;

/// <summary>Nomes das claims de domínio do token (contracts/usuarios-carteiras-api.md).</summary>
public static class ClaimsToken
{
    public const string Sub = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string TipoConta = "tipo_conta";
    public const string CarteiraId = "carteira_id";

    /// <summary>Sessão de origem do token (specs/016-sessao-refresh-token, research R5).</summary>
    public const string Sessao = JwtRegisteredClaimNames.Sid;
}

/// <summary>Emite o JWT HS256 com as claims do contrato (FR-017, FR-018; research R4) e a sessão (spec 016).</summary>
internal sealed class EmissorTokenJwt(IOptions<JwtOptions> opcoes, TimeProvider relogio) : IEmissorToken
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenEmitido Emitir(Usuario usuario, SessaoId sessaoId)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var configuracao = opcoes.Value;
        var agora = relogio.GetUtcNow().UtcDateTime;

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = configuracao.Emissor,
            Audience = configuracao.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.AddSeconds(configuracao.ExpiracaoSegundos),
            SigningCredentials = new SigningCredentials(configuracao.CriarChave(), JwtOptions.Algoritmo),
            Claims = new Dictionary<string, object>
            {
                [ClaimsToken.Sub] = usuario.Id.ToString(),
                [ClaimsToken.Email] = usuario.Email,
                [ClaimsToken.TipoConta] = usuario.TipoConta.ToString(),
                [ClaimsToken.CarteiraId] = usuario.Carteira.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [ClaimsToken.Sessao] = sessaoId.ToString(),
            },
        };

        return new TokenEmitido(_handler.CreateToken(descritor), configuracao.ExpiracaoSegundos);
    }
}
