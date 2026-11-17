using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bank.IntegrationTests;

/// <summary>Forja tokens (válidos e inválidos) para os cenários de 401/403.</summary>
public static class TokensDeTeste
{
    public static string Criar(
        string chave,
        IDictionary<string, object> claims,
        DateTime? emitidoEm = null,
        DateTime? expiraEm = null,
        string algoritmo = SecurityAlgorithms.HmacSha256,
        string emissor = "bank-api",
        string audiencia = "bank-api",
        bool assinar = true)
    {
        var emissao = emitidoEm ?? DateTime.UtcNow;
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = audiencia,
            IssuedAt = emissao,
            NotBefore = emissao,
            Expires = expiraEm ?? emissao.AddHours(1),
            Claims = claims,
            SigningCredentials = assinar
                ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), algoritmo)
                : null,
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descritor);
    }

    public static Dictionary<string, object> Claims(string usuarioId, string carteiraId, string tipoConta = "PessoaFisica") => new()
    {
        ["sub"] = usuarioId,
        ["email"] = "teste@exemplo.com.br",
        ["tipo_conta"] = tipoConta,
        ["carteira_id"] = carteiraId,
    };
}
