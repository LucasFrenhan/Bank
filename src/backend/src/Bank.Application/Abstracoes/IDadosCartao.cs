using Bank.Domain.Cartoes;

namespace Bank.Application.Abstracoes;

/// <summary>Dados derivados de um cartão; os tipos mascaram a própria saída de texto (research R2).</summary>
public sealed record DadosDerivados(NumeroCartao Numero, Cvv Cvv, string ImpressaoDigital);

/// <summary>
/// Deriva número e CVV, que nunca são gravados (specs/014-cartao-credito-cobrancas, FR-007, FR-008;
/// research R1, R3).
/// </summary>
public interface IDadosCartao
{
    DadosDerivados Derivar(CartaoId cartaoId, Bandeira bandeira, int variacao, ValidadeCartao validade);

    /// <summary>Dados de um cartão emitido; falha se a impressão digital gravada não corresponder (chave trocada).</summary>
    DadosDerivados Recuperar(Cartao cartao);

    /// <summary>Compara número, validade e CVV em tempo constante, sem indicar qual diverge.</summary>
    bool Conferem(Cartao cartao, NumeroCartao numero, ValidadeCartao validade, Cvv cvv);
}
