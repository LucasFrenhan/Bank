using Bank.Domain.Bancos;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bank.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Conversões e restrições comuns às tabelas com dados bancários (<c>carteiras</c> e
/// <c>contas_externas</c>; specs/012-dados-bancarios-carteira, data-model).
/// </summary>
internal static class ConversoresBancarios
{
    public const string ContaCorrente = "conta_corrente";
    public const string ContaPoupanca = "conta_poupanca";
    public const int TamanhoMaximoTipo = 20;

    public static readonly ValueConverter<TipoContaBancaria, string> Tipo = new(
        tipo => tipo == TipoContaBancaria.ContaPoupanca ? ContaPoupanca : ContaCorrente,
        texto => texto == ContaPoupanca ? TipoContaBancaria.ContaPoupanca : TipoContaBancaria.ContaCorrente);

    public static string CheckTipo => $"tipo_conta_bancaria IN ('{ContaCorrente}', '{ContaPoupanca}')";

    public const string CheckFormato =
        "agencia ~ '^[0-9]{4}$' AND numero_conta ~ '^[0-9]{8}$' AND digito_conta ~ '^[0-9]$'";

    public const string CheckDigito = "digito_conta = calcular_digito_conta(agencia, numero_conta)";
}
