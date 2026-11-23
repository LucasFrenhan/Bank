namespace Bank.Domain.Bancos;

/// <summary>
/// Tipo da conta bancária (specs/012-dados-bancarios-carteira, FR-002). O sistema cria sempre
/// conta corrente; poupança só por script nesta entrega (FR-006, FR-010).
/// </summary>
public enum TipoContaBancaria
{
    ContaCorrente = 1,
    ContaPoupanca = 2,
}
