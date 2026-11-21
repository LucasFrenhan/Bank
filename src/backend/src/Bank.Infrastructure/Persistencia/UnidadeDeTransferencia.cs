using System.Data;
using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Infrastructure.Mensageria;
using Bank.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Transação de uma transferência (specs/006-transferencias, research R3): <c>ReadCommitted</c>,
/// bloqueio pessimista das carteiras em ordem de identificador (<c>FOR UPDATE</c>, em arquivo
/// <c>.sql</c>), e transferência + lançamentos + evento (outbox) num único <c>SaveChanges</c>.
/// </summary>
internal sealed class UnidadeDeTransferencia(BankDbContext contexto) : IUnidadeDeTransferencia, IAsyncDisposable
{
    public const string ArquivoBloqueio = "Consultas/BloquearCarteirasParaTransferencia.sql";

    private IDbContextTransaction? _transacao;

    public async Task IniciarAsync(CancellationToken cancellationToken)
    {
        if (_transacao is not null)
        {
            throw new InvalidOperationException("Já existe uma transação de transferência em andamento.");
        }

        _transacao = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task<IReadOnlyList<Carteira>> BloquearCarteirasAsync(CarteiraId carteiraA, CarteiraId carteiraB, CancellationToken cancellationToken)
    {
        if (_transacao is null)
        {
            throw new InvalidOperationException("O bloqueio exige uma transação iniciada.");
        }

        // A ordem também está no SQL (ORDER BY id); os ids seguem ordenados por clareza. Parâmetro de
        // SQL puro: o conversor do EF não atua aqui, então vai o Guid (issue #43).
        Guid[] ids = carteiraA.Valor.CompareTo(carteiraB.Valor) <= 0 ? [carteiraA.Valor, carteiraB.Valor] : [carteiraB.Valor, carteiraA.Valor];

        return await contexto.Carteiras
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoBloqueio), new NpgsqlParameter("ids", ids))
            .ToListAsync(cancellationToken);
    }

    public void Adicionar(ExecucaoTransferencia execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        contexto.Transferencias.Add(execucao.Transferencia);
        contexto.Lancamentos.AddRange(execucao.Saida, execucao.Entrada);
        contexto.EventosOutbox.Add(EventoOutbox.De(execucao.Evento));
    }

    public async Task<ResultadoConfirmacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        if (_transacao is null)
        {
            throw new InvalidOperationException("Não há transação para confirmar.");
        }

        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
            await _transacao.CommitAsync(cancellationToken);
            await EncerrarAsync();
            return ResultadoConfirmacao.Ok;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TransferenciaConfiguracao.IndiceChave,
        })
        {
            await DesfazerAsync(cancellationToken);
            return ResultadoConfirmacao.ChaveDuplicada;
        }
    }

    public async Task DesfazerAsync(CancellationToken cancellationToken)
    {
        if (_transacao is not null)
        {
            await _transacao.RollbackAsync(cancellationToken);
            await EncerrarAsync();
        }

        contexto.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync() => await EncerrarAsync();

    private async Task EncerrarAsync()
    {
        if (_transacao is not null)
        {
            await _transacao.DisposeAsync();
            _transacao = null;
        }
    }
}
