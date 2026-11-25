namespace Bank.Application.Abstracoes;

public enum ResultadoGravacao
{
    Ok,

    /// <summary>A chave de idempotência foi gravada por outra requisição simultânea.</summary>
    ChaveDuplicada,

    /// <summary>O usuário já tem cartão (solicitações simultâneas).</summary>
    CartaoDoUsuarioDuplicado,

    /// <summary>O número derivado já pertence a outro cartão.</summary>
    NumeroDeCartaoDuplicado,

    /// <summary>O código sorteado já pertence a outra cobrança (specs/015-frontend-cartao-credito, research R1).</summary>
    CodigoDeCobrancaDuplicado,
}

/// <summary>
/// Transação dos casos de uso da specs/014-cartao-credito-cobrancas (research R9): <c>ReadCommitted</c>,
/// com bloqueios pelos repositórios na ordem cobrança → cartão → recebível → carteira (R8).
/// </summary>
public interface IUnidadeDeTrabalho
{
    Task IniciarAsync(CancellationToken cancellationToken);

    /// <summary>Grava e confirma; numa duplicidade esperada, desfaz tudo e informa qual.</summary>
    Task<ResultadoGravacao> ConfirmarAsync(CancellationToken cancellationToken);

    /// <summary>Desfaz a transação em andamento (sem efeito se não houver).</summary>
    Task DesfazerAsync(CancellationToken cancellationToken);
}
