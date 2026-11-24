using System.Data;
using Bank.Application.Abstracoes;
using Bank.Infrastructure.Persistencia.Configuracoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Transação <c>ReadCommitted</c> dos casos de uso de cartão (specs/014-cartao-credito-cobrancas, research R9).</summary>
internal sealed class UnidadeDeTrabalho(BankDbContext contexto) : IUnidadeDeTrabalho, IAsyncDisposable
{
    private IDbContextTransaction? _transacao;

    public async Task IniciarAsync(CancellationToken cancellationToken)
    {
        if (_transacao is not null)
        {
            throw new InvalidOperationException("Já existe uma transação em andamento.");
        }

        _transacao = await contexto.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task<ResultadoGravacao> ConfirmarAsync(CancellationToken cancellationToken)
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
            contexto.ChangeTracker.Clear();
            return ResultadoGravacao.Ok;
        }
        catch (DbUpdateException ex) when (Duplicidade(ex) is { } duplicidade)
        {
            await DesfazerAsync(cancellationToken);
            return duplicidade;
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

    private static ResultadoGravacao? Duplicidade(DbUpdateException excecao) =>
        excecao.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violacao
            ? violacao.ConstraintName switch
            {
                AutorizacaoCartaoConfiguracao.IndiceChave or FaturaConfiguracao.IndiceChave or RecebivelConfiguracao.IndiceChave => ResultadoGravacao.ChaveDuplicada,
                CartaoConfiguracao.IndiceUsuario => ResultadoGravacao.CartaoDoUsuarioDuplicado,
                CartaoConfiguracao.IndiceImpressaoDigital => ResultadoGravacao.NumeroDeCartaoDuplicado,
                CobrancaConfiguracao.IndiceCodigo => ResultadoGravacao.CodigoDeCobrancaDuplicado,
                _ => null,
            }
            : null;

    private async Task EncerrarAsync()
    {
        if (_transacao is not null)
        {
            await _transacao.DisposeAsync();
            _transacao = null;
        }
    }
}
