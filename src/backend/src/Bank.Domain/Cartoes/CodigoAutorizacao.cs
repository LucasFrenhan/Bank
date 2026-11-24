using System.Security.Cryptography;

namespace Bank.Domain.Cartoes;

/// <summary>Código de autorização da bandeira simulada: 6 caracteres de A-Z e 0-9 (specs/014-cartao-credito-cobrancas, FR-017).</summary>
public static class CodigoAutorizacao
{
    public const int Tamanho = 6;

    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string Novo() => RandomNumberGenerator.GetString(Alfabeto, Tamanho);
}
