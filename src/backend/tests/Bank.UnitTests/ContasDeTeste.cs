using Bank.Domain.Bancos;

namespace Bank.UnitTests;

/// <summary>Dados bancários prontos para os testes que criam usuários (spec 012).</summary>
internal static class ContasDeTeste
{
    public static ContaBancaria Corrente(long numero = 1) =>
        ContaBancaria.Nova(Banco.OneBank, ContaBancaria.AgenciaPadrao, numero, TipoContaBancaria.ContaCorrente);
}
