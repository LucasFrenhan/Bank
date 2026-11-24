using Bank.Domain.Cartoes;
using Bank.Domain.Comum;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Cobrancas;

/// <summary>
/// Fato publicado após cada cobrança paga (evento <c>cobranca.paga</c> v1; specs/014-cartao-credito-cobrancas,
/// FR-021; contracts/evento-cobranca-paga.md).
/// </summary>
public sealed record CobrancaPaga(
    EventoId EventoId,
    CobrancaId CobrancaId,
    UsuarioId LojistaUsuarioId,
    UsuarioId PagadorUsuarioId,
    decimal Valor,
    string UltimosDigitosCartao,
    DateTimeOffset OcorridoEm,
    string LojistaNome)
{
    public const string Tipo = "cobranca.paga";
    public const int Versao = 1;
}

/// <summary>Tudo o que um pagamento aprovado grava, numa única transação (FR-018).</summary>
public sealed record ExecucaoPagamento(
    AutorizacaoCartao Autorizacao,
    CompraCartao Compra,
    Recebivel Recebivel,
    CobrancaPaga Evento);

/// <summary>
/// Efeitos de uma aprovação da bandeira (FR-018): cobrança paga, limite consumido, autorização, compra na
/// fatura do ciclo, recebível do lojista e evento. Nenhum saldo muda. Cobrança e cartão chegam bloqueados.
/// </summary>
public static class PagamentoCobranca
{
    public static Resultado<ExecucaoPagamento> Executar(
        Cobranca cobranca,
        Cartao cartao,
        Fatura fatura,
        string lojistaNome,
        DateOnly dataCompra,
        Guid chaveIdempotencia,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cobranca);
        ArgumentNullException.ThrowIfNull(cartao);
        ArgumentNullException.ThrowIfNull(fatura);

        if (cobranca.Status != StatusCobranca.Pendente)
        {
            return Resultado.Falha<ExecucaoPagamento>(ErrosCobranca.NaoPendente);
        }

        var limite = cartao.ConsumirLimite(cobranca.Valor);
        if (!limite.Sucesso)
        {
            return Resultado.Falha<ExecucaoPagamento>(limite.Erros);
        }

        var autorizacao = AutorizacaoCartao.Aprovar(cobranca, cartao, chaveIdempotencia, agora);
        cobranca.MarcarPaga(autorizacao);

        return Resultado.Ok(new ExecucaoPagamento(
            autorizacao,
            CompraCartao.De(autorizacao, cobranca, fatura, lojistaNome, dataCompra, agora),
            Recebivel.Criar(cobranca, dataCompra, agora),
            new CobrancaPaga(
                EventoId.Novo(agora), cobranca.Id, cobranca.LojistaUsuarioId, cartao.UsuarioId, cobranca.Valor,
                cartao.UltimosDigitos, agora, lojistaNome)));
    }
}
