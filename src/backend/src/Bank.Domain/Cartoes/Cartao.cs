using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Cartoes;

/// <summary>
/// Cartão de crédito da Pessoa Física, um por usuário e com bandeira imutável
/// (specs/014-cartao-credito-cobrancas, FR-002 a FR-006). Número e CVV não são gravados: são
/// derivados quando necessários (research R1). O limite utilizado muda só com o cartão bloqueado (R4).
/// </summary>
public sealed class Cartao
{
    public const int MaximoVariacao = 4;

    private Cartao()
    {
    }

    public CartaoId Id { get; private set; }

    public UsuarioId UsuarioId { get; private set; }

    public string CodigoBandeira { get; private set; } = string.Empty;

    public string NomeImpresso { get; private set; } = string.Empty;

    public string UltimosDigitos { get; private set; } = string.Empty;

    public int ValidadeAno { get; private set; }

    public int ValidadeMes { get; private set; }

    /// <summary>Entra na derivação do número; cresce se o número anterior já existir (R1).</summary>
    public int Variacao { get; private set; }

    /// <summary>HMAC do número: garante unicidade sem permitir recuperá-lo (R1).</summary>
    public string ImpressaoDigital { get; private set; } = string.Empty;

    public decimal LimiteTotal { get; private set; }

    public decimal LimiteUtilizado { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public decimal LimiteDisponivel => LimiteTotal - LimiteUtilizado;

    public ValidadeCartao Validade => ValidadeCartao.Reconstituir(ValidadeAno, ValidadeMes);

    public static Cartao Emitir(
        CartaoId id,
        UsuarioId usuarioId,
        Bandeira bandeira,
        string nomeTitular,
        ValidadeCartao validade,
        int variacao,
        string ultimosDigitos,
        string impressaoDigital,
        decimal limiteTotal,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(bandeira);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeTitular);
        ArgumentNullException.ThrowIfNull(validade);
        ArgumentException.ThrowIfNullOrWhiteSpace(ultimosDigitos);
        ArgumentException.ThrowIfNullOrWhiteSpace(impressaoDigital);
        ArgumentOutOfRangeException.ThrowIfNegative(variacao);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variacao, MaximoVariacao);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limiteTotal);

        return new Cartao
        {
            Id = id,
            UsuarioId = usuarioId,
            CodigoBandeira = bandeira.Codigo,
            NomeImpresso = nomeTitular.Trim().ToUpperInvariant(),
            UltimosDigitos = ultimosDigitos,
            ValidadeAno = validade.Ano,
            ValidadeMes = validade.Mes,
            Variacao = variacao,
            ImpressaoDigital = impressaoDigital,
            LimiteTotal = Dinheiro.NormalizarEscala(limiteTotal),
            LimiteUtilizado = 0.00m,
            CriadoEm = agora,
        };
    }

    /// <summary>Consome o limite; recusa sem alterar nada se o disponível não cobrir.</summary>
    public Resultado ConsumirLimite(decimal valor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        if (valor > LimiteDisponivel)
        {
            return Resultado.Falha(ErrosCartao.LimiteInsuficiente);
        }

        LimiteUtilizado = Dinheiro.NormalizarEscala(LimiteUtilizado + valor);
        return Resultado.Ok();
    }

    /// <summary>Devolve ao limite o total de uma fatura paga (sem os juros).</summary>
    public void LiberarLimite(decimal valor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);
        if (valor > LimiteUtilizado)
        {
            throw new InvalidOperationException("O valor a liberar excede o limite utilizado.");
        }

        LimiteUtilizado = Dinheiro.NormalizarEscala(LimiteUtilizado - valor);
    }
}
