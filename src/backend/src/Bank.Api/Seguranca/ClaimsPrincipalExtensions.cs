using System.Security.Claims;
using Bank.Infrastructure.Seguranca;

namespace Bank.Api.Seguranca;

/// <summary>Leitura do titular a partir do token (specs/005-usuarios-me, research R2).</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Lê a claim <c>sub</c>; falso se ausente ou se não for um identificador válido.</summary>
    public static bool TentarObterUsuarioId(this ClaimsPrincipal usuario, out Guid usuarioId)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return Guid.TryParse(usuario.FindFirstValue(ClaimsToken.Sub), out usuarioId);
    }
}
