using Bank.Domain.Calendario;
using Bank.Domain.Carteiras;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Recebiveis;

public enum SituacaoRecebivel
{
    Pendente = 1,
    Liquidado = 2,
    Antecipado = 3,
}

/// <summary>
/// Direito do lojista sobre uma cobrança paga (specs/014-cartao-credito-cobrancas, FR-030 a FR-032):
/// valor integral no 30º dia útil ou, antes disso, antecipado com taxa fixa de 2%.
/// </summary>
public sealed class Recebivel
{
    public const int PrazoDiasUteis = 30;
    public const decimal TaxaPercentual = 2m;

    private Recebivel()
    {
    }

    public RecebivelId Id { get; private set; }

    public CobrancaId CobrancaId { get; private set; }

    public UsuarioId LojistaUsuarioId { get; private set; }

    public CarteiraId CarteiraId { get; private set; }

    public decimal ValorBruto { get; private set; }

    public decimal Taxa { get; private set; }

    public decimal ValorLiquido { get; private set; }

    public DateOnly DataPrevista { get; private set; }

    public SituacaoRecebivel Situacao { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? RecebidoEm { get; private set; }

    public Guid? ChaveIdempotencia { get; private set; }

    public static Recebivel Criar(Cobranca cobranca, DateOnly dataCompra, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cobranca);

        return new Recebivel
        {
            Id = RecebivelId.Novo(agora),
            CobrancaId = cobranca.Id,
            LojistaUsuarioId = cobranca.LojistaUsuarioId,
            CarteiraId = cobranca.CarteiraDestinoId,
            ValorBruto = cobranca.Valor,
            Taxa = 0.00m,
            ValorLiquido = cobranca.Valor,
            DataPrevista = CalendarioBancario.SomarDiasUteis(dataCompra, PrazoDiasUteis),
            Situacao = SituacaoRecebivel.Pendente,
            CriadoEm = agora,
        };
    }

    /// <summary>Marca como liquidado na data prevista ou depois; falso se não houver o que liquidar.</summary>
    public bool Liquidar(DateOnly hoje, DateTimeOffset agora)
    {
        if (Situacao != SituacaoRecebivel.Pendente || hoje < DataPrevista)
        {
            return false;
        }

        Situacao = SituacaoRecebivel.Liquidado;
        RecebidoEm = agora;
        return true;
    }

    /// <summary>Antecipa antes da data prevista, com a taxa descontada do valor.</summary>
    public Resultado Antecipar(DateOnly hoje, Guid chaveIdempotencia, DateTimeOffset agora)
    {
        if (Situacao != SituacaoRecebivel.Pendente || hoje >= DataPrevista)
        {
            return Resultado.Falha(ErrosRecebivel.NaoAntecipavel);
        }

        Taxa = decimal.Round(ValorBruto * TaxaPercentual / 100m, 2, MidpointRounding.AwayFromZero) + 0.00m;
        ValorLiquido = ValorBruto - Taxa;
        Situacao = SituacaoRecebivel.Antecipado;
        RecebidoEm = agora;
        ChaveIdempotencia = chaveIdempotencia;
        return Resultado.Ok();
    }
}
