using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Pagamento já aprovado com uma chave de idempotência (specs/014-cartao-credito-cobrancas, FR-019).</summary>
public sealed record PagamentoAprovado(
    CobrancaId CobrancaId,
    decimal Valor,
    string CodigoBandeira,
    string UltimosDigitos,
    string CodigoAutorizacao,
    string LojistaNome,
    DateTimeOffset PagaEm);

/// <summary>Persistência de cobranças e do pagamento (specs/014-cartao-credito-cobrancas, US2 e US3).</summary>
public interface ICobrancaRepositorio
{
    void Adicionar(Cobranca cobranca);

    /// <summary>Bloqueia a cobrança; exige transação.</summary>
    Task<Cobranca?> BloquearAsync(CobrancaId cobrancaId, CancellationToken cancellationToken);

    /// <summary>Registra uma decisão da bandeira (recusa).</summary>
    void Adicionar(AutorizacaoCartao autorizacao);

    /// <summary>Registra autorização, compra, recebível e evento (outbox) de uma aprovação.</summary>
    void Adicionar(ExecucaoPagamento execucao);

    Task<PagamentoAprovado?> ObterPagamentoPorChaveAsync(UsuarioId pagador, Guid chaveIdempotencia, CancellationToken cancellationToken);
}
