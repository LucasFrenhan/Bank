using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Cartoes;

public enum ResultadoAutorizacao
{
    Aprovada = 1,
    Recusada = 2,
}

public enum MotivoRecusa
{
    CartaoNaoAutorizado = 1,
    FaturaVencida = 2,
    LimiteInsuficiente = 3,
}

/// <summary>
/// Autorizador simulado da bandeira (specs/014-cartao-credito-cobrancas, FR-016; research R11):
/// decide nesta ordem — dados do cartão, fatura vencida, limite.
/// </summary>
public static class AutorizadorBandeira
{
    /// <summary>Motivo da recusa, ou nulo se aprovada.</summary>
    public static MotivoRecusa? Decidir(bool dadosConferem, bool temFaturaVencida, decimal limiteDisponivel, decimal valor)
    {
        if (!dadosConferem)
        {
            return MotivoRecusa.CartaoNaoAutorizado;
        }

        if (temFaturaVencida)
        {
            return MotivoRecusa.FaturaVencida;
        }

        return valor > limiteDisponivel ? MotivoRecusa.LimiteInsuficiente : null;
    }

    public static Erro ErroDe(MotivoRecusa motivo) => motivo switch
    {
        MotivoRecusa.CartaoNaoAutorizado => ErrosCartao.NaoAutorizado,
        MotivoRecusa.FaturaVencida => ErrosCartao.BloqueadoPorFaturaVencida,
        MotivoRecusa.LimiteInsuficiente => ErrosCartao.LimiteInsuficiente,
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Motivo de recusa desconhecido."),
    };
}

/// <summary>
/// Decisão imutável da bandeira sobre uma tentativa de pagamento (specs/014-cartao-credito-cobrancas,
/// FR-017). Guarda só a bandeira e os 4 últimos dígitos (FR-008).
/// </summary>
public sealed class AutorizacaoCartao
{
    private AutorizacaoCartao()
    {
    }

    public AutorizacaoCartaoId Id { get; private set; }

    public CobrancaId CobrancaId { get; private set; }

    public UsuarioId PagadorUsuarioId { get; private set; }

    /// <summary>Nulo quando os dados informados não são do cartão do pagador.</summary>
    public CartaoId? CartaoId { get; private set; }

    /// <summary>Nulo quando o número não é de uma bandeira conhecida.</summary>
    public string? CodigoBandeira { get; private set; }

    public string UltimosDigitos { get; private set; } = string.Empty;

    public decimal Valor { get; private set; }

    public ResultadoAutorizacao Resultado { get; private set; }

    public MotivoRecusa? Motivo { get; private set; }

    public string? CodigoAutorizacao { get; private set; }

    public Guid ChaveIdempotencia { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public static AutorizacaoCartao Aprovar(Cobranca cobranca, Cartao cartao, Guid chaveIdempotencia, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cobranca);
        ArgumentNullException.ThrowIfNull(cartao);

        return new AutorizacaoCartao
        {
            Id = AutorizacaoCartaoId.Novo(agora),
            CobrancaId = cobranca.Id,
            PagadorUsuarioId = cartao.UsuarioId,
            CartaoId = cartao.Id,
            CodigoBandeira = cartao.CodigoBandeira,
            UltimosDigitos = cartao.UltimosDigitos,
            Valor = cobranca.Valor,
            Resultado = ResultadoAutorizacao.Aprovada,
            CodigoAutorizacao = Cartoes.CodigoAutorizacao.Novo(),
            ChaveIdempotencia = chaveIdempotencia,
            CriadoEm = agora,
        };
    }

    public static AutorizacaoCartao Recusar(
        Cobranca cobranca,
        UsuarioId pagadorUsuarioId,
        CartaoId? cartaoId,
        NumeroCartao numeroInformado,
        MotivoRecusa motivo,
        Guid chaveIdempotencia,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cobranca);
        ArgumentNullException.ThrowIfNull(numeroInformado);

        return new AutorizacaoCartao
        {
            Id = AutorizacaoCartaoId.Novo(agora),
            CobrancaId = cobranca.Id,
            PagadorUsuarioId = pagadorUsuarioId,
            CartaoId = cartaoId,
            CodigoBandeira = Bandeira.DoNumero(numeroInformado)?.Codigo,
            UltimosDigitos = numeroInformado.UltimosDigitos,
            Valor = cobranca.Valor,
            Resultado = ResultadoAutorizacao.Recusada,
            Motivo = motivo,
            ChaveIdempotencia = chaveIdempotencia,
            CriadoEm = agora,
        };
    }
}
