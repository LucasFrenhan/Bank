using Bank.Domain.Bancos;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Domain.Carteiras;

/// <summary>
/// Carteira digital do usuário (1:1). Saldo nunca negativo, com escala 2, em BRL
/// (RN-04, FR-014 a FR-016). Movimentações chegam com a spec de transferências (issue #8).
/// </summary>
public sealed class Carteira
{
    private Carteira()
    {
    }

    public CarteiraId Id { get; private set; }

    public UsuarioId UsuarioId { get; private set; }

    public decimal Saldo { get; private set; }

    public string Moeda { get; private set; } = Dinheiro.MoedaPadrao;

    public DateTimeOffset AtualizadoEm { get; private set; }

    /// <summary>Código do banco; sempre 001 para carteiras (specs/012-dados-bancarios-carteira, FR-014).</summary>
    public string CodigoBanco { get; private set; } = string.Empty;

    public string Agencia { get; private set; } = string.Empty;

    public string NumeroConta { get; private set; } = string.Empty;

    public string DigitoConta { get; private set; } = string.Empty;

    public TipoContaBancaria TipoContaBancaria { get; private set; }

    /// <summary>Dados bancários da conta (não mudam depois da criação).</summary>
    public ContaBancaria ContaBancaria =>
        ContaBancaria.Reconstituir(CodigoBanco, Agencia, NumeroConta, DigitoConta, TipoContaBancaria);

    internal static Carteira Criar(UsuarioId usuarioId, ContaBancaria conta, decimal saldoInicial, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(conta);
        if (conta.CodigoBanco != Banco.CodigoOneBank)
        {
            throw new ArgumentException($"Carteiras pertencem sempre ao banco {Banco.CodigoOneBank}.", nameof(conta));
        }

        if (saldoInicial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(saldoInicial), saldoInicial, "O saldo inicial não pode ser negativo.");
        }

        return new Carteira
        {
            Id = CarteiraId.Novo(agora),
            UsuarioId = usuarioId,
            Saldo = Dinheiro.NormalizarEscala(saldoInicial),
            Moeda = Dinheiro.MoedaPadrao,
            AtualizadoEm = agora,
            CodigoBanco = conta.CodigoBanco,
            Agencia = conta.Agencia,
            NumeroConta = conta.Numero,
            DigitoConta = conta.Digito,
            TipoContaBancaria = conta.Tipo,
        };
    }

    /// <summary>Debita o valor; recusa sem alterar nada se o saldo não cobrir (RN-05 da #8).</summary>
    public Resultado Debitar(decimal valor, DateTimeOffset agora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        if (Saldo < valor)
        {
            return Resultado.Falha(Transferencias.ErrosTransferencia.SaldoInsuficiente);
        }

        Saldo = Dinheiro.NormalizarEscala(Saldo - valor);
        AtualizadoEm = agora;
        return Resultado.Ok();
    }

    public void Creditar(decimal valor, DateTimeOffset agora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        Saldo = Dinheiro.NormalizarEscala(Saldo + valor);
        AtualizadoEm = agora;
    }
}
