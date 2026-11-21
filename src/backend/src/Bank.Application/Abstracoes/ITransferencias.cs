using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Pagador; o documento sai mascarado nas respostas (specs/008, FR-002a).</summary>
public sealed record PagadorComprovante(UsuarioId UsuarioId, string Nome, string Documento);

public sealed record RecebedorComprovante(UsuarioId UsuarioId, string Nome, string Documento, TipoConta TipoConta);

/// <summary>
/// Comprovante de uma transferência concluída (spec 006, FR-009; contrato v1.1.0 da spec 008).
/// A chave é sempre preenchida no envio e nula na consulta feita pelo recebedor (spec 008, FR-003).
/// </summary>
public sealed record Comprovante(
    TransferenciaId TransferenciaId,
    Guid? ChaveIdempotencia,
    PagadorComprovante Pagador,
    RecebedorComprovante Recebedor,
    decimal Valor,
    string? Descricao,
    StatusTransferencia Status,
    DateTimeOffset CriadoEm);

/// <summary>
/// Leitura de transferências já gravadas (idempotência e consulta). Os documentos do pagador e do
/// recebedor vêm sem máscara; a Application aplica a máscara de exibição.
/// </summary>
public interface ITransferenciaRepositorio
{
    /// <summary>Transferência do pagador com a chave informada, se existir (spec 006, FR-011).</summary>
    Task<Comprovante?> ObterComprovantePorChaveAsync(CarteiraId carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken);

    /// <summary>Transferência pelo identificador, se existir (specs/008-consulta-transferencia).</summary>
    Task<Comprovante?> ObterComprovantePorIdAsync(TransferenciaId transferenciaId, CancellationToken cancellationToken);

    /// <summary>
    /// Soma das transferências concluídas enviadas pela carteira com <c>criado_em</c> em
    /// <c>[inicioUtc, fimUtc)</c>; 0 se não houver (specs/009-limite-transferencia, R3). No envio, é
    /// chamada dentro da transação, depois do bloqueio da carteira de origem.
    /// </summary>
    Task<decimal> SomarEnviadasAsync(CarteiraId carteiraOrigemId, DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken cancellationToken);
}

public enum ResultadoConfirmacao
{
    Ok,

    /// <summary>A chave de idempotência foi gravada por outra requisição simultânea.</summary>
    ChaveDuplicada,
}

/// <summary>
/// Transação de uma transferência (specs/006-transferencias, research R3): bloqueio das
/// carteiras em ordem determinística, gravação atômica e confirmação. Implementada na
/// Infrastructure; as regras ficam no domínio.
/// </summary>
public interface IUnidadeDeTransferencia
{
    Task IniciarAsync(CancellationToken cancellationToken);

    /// <summary>Bloqueia as carteiras na ordem do identificador; devolve só as que existem.</summary>
    Task<IReadOnlyList<Carteira>> BloquearCarteirasAsync(CarteiraId carteiraA, CarteiraId carteiraB, CancellationToken cancellationToken);

    /// <summary>Registra transferência, lançamentos e evento (outbox) para gravação.</summary>
    void Adicionar(ExecucaoTransferencia execucao);

    Task<ResultadoConfirmacao> ConfirmarAsync(CancellationToken cancellationToken);

    /// <summary>Desfaz a transação em andamento (sem efeito se não houver).</summary>
    Task DesfazerAsync(CancellationToken cancellationToken);
}
