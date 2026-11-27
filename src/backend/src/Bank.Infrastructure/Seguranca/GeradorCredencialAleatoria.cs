using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstracoes;
using Bank.Domain.Sessoes;

namespace Bank.Infrastructure.Seguranca;

/// <summary>
/// Credencial de renovação: 32 bytes aleatórios em Base64Url (43 caracteres); o banco guarda só o SHA-256
/// (specs/016-sessao-refresh-token, FR-003, FR-004; research R1).
/// </summary>
internal sealed class GeradorCredencialAleatoria : IGeradorCredencial
{
    public const int TamanhoBytes = 32;

    public CredencialGerada Gerar()
    {
        var valor = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TamanhoBytes));
        return new CredencialGerada(valor, CalcularHash(valor));
    }

    public HashCredencial CalcularHash(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    }
}
