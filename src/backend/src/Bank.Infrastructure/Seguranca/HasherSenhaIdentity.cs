using Bank.Application.Abstracoes;
using Bank.Domain.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace Bank.Infrastructure.Seguranca;

/// <summary>
/// Hash de senha do ASP.NET Core Identity (formato V3: PBKDF2-HMAC-SHA512, sal de 128 bits,
/// 100.000 iterações) — research R3. O usuário não é usado pelo algoritmo.
/// </summary>
internal sealed class HasherSenhaIdentity : IHasherSenha
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string GerarHash(string senha) => _hasher.HashPassword(null!, senha);

    public ResultadoVerificacaoSenha Verificar(string hash, string senha) =>
        _hasher.VerifyHashedPassword(null!, hash, senha) switch
        {
            PasswordVerificationResult.Success => ResultadoVerificacaoSenha.Sucesso,
            PasswordVerificationResult.SuccessRehashNeeded => ResultadoVerificacaoSenha.SucessoRehashNecessario,
            _ => ResultadoVerificacaoSenha.Falha,
        };
}
