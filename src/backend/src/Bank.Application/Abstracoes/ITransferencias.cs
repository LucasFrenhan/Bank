using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

public sealed record PagadorComprovante(Guid UsuarioId, string Nome);

public sealed record RecebedorComprovante(Guid UsuarioId, string Nome, string Documento, TipoConta TipoConta);

/// <summary>Comprovante de uma transferência concluída (FR-009; contracts/transferencias-api.md).</summary>
public sealed record Comprovante(
    Guid TransferenciaId,
    Guid ChaveIdempotencia,
    PagadorComprovante Pagador,
    RecebedorComprovante Recebedor,
    decimal Valor,
    string? Descricao,
    StatusTransferencia Status,
    DateTimeOffset CriadoEm);

/// <summary>Leitura de transferências já gravadas (idempotência).</summary>
public interface ITransferenciaRepositorio
{
    /// <summary>Transferência do pagador com a chave informada, se existir (FR-011).</summary>
    Task<Comprovante?> ObterComprovantePorChaveAsync(Guid carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken);
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
    Task<IReadOnlyList<Carteira>> BloquearCarteirasAsync(Guid carteiraA, Guid carteiraB, CancellationToken cancellationToken);

    /// <summary>Registra transferência, lançamentos e evento (outbox) para gravação.</summary>
    void Adicionar(ExecucaoTransferencia execucao);

    Task<ResultadoConfirmacao> ConfirmarAsync(CancellationToken cancellationToken);

    /// <summary>Desfaz a transação em andamento (sem efeito se não houver).</summary>
    Task DesfazerAsync(CancellationToken cancellationToken);
}
