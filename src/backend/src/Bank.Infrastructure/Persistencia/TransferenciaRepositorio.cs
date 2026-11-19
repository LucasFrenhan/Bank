using Bank.Application.Abstracoes;
using Bank.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Leitura de transferências gravadas: idempotência (specs/006-transferencias, R4) e consulta por
/// identificador (specs/008-consulta-transferencia, R1) e soma do dia (specs/009, R3). Uma única projeção garante o mesmo
/// comprovante nos dois casos; os documentos saem sem máscara.
/// </summary>
internal sealed class TransferenciaRepositorio(BankDbContext contexto) : ITransferenciaRepositorio
{
    public Task<Comprovante?> ObterComprovantePorChaveAsync(Guid carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken) =>
        ObterAsync(contexto.Transferencias.Where(t => t.CarteiraOrigemId == carteiraOrigemId && t.ChaveIdempotencia == chaveIdempotencia), cancellationToken);

    public Task<Comprovante?> ObterComprovantePorIdAsync(Guid transferenciaId, CancellationToken cancellationToken) =>
        ObterAsync(contexto.Transferencias.Where(t => t.Id == transferenciaId), cancellationToken);

    public async Task<decimal> SomarEnviadasAsync(Guid carteiraOrigemId, DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken cancellationToken) =>
        await contexto.Transferencias
            .Where(t => t.CarteiraOrigemId == carteiraOrigemId && t.CriadoEm >= inicioUtc && t.CriadoEm < fimUtc)
            .SumAsync(t => (decimal?)t.Valor, cancellationToken) ?? 0m;

    private async Task<Comprovante?> ObterAsync(IQueryable<Transferencia> transferencias, CancellationToken cancellationToken)
    {
        var dados = await (
            from t in transferencias.AsNoTracking()
            join origem in contexto.Carteiras on t.CarteiraOrigemId equals origem.Id
            join pagador in contexto.Usuarios on origem.UsuarioId equals pagador.Id
            join destino in contexto.Carteiras on t.CarteiraDestinoId equals destino.Id
            join recebedor in contexto.Usuarios on destino.UsuarioId equals recebedor.Id
            select new
            {
                Transferencia = t,
                PagadorId = pagador.Id,
                PagadorNome = pagador.NomeCompleto,
                PagadorDocumento = pagador.Documento,
                RecebedorId = recebedor.Id,
                RecebedorNome = recebedor.NomeCompleto,
                RecebedorDocumento = recebedor.Documento,
                recebedor.TipoConta,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (dados is null)
        {
            return null;
        }

        var transferencia = dados.Transferencia;
        return new Comprovante(
            transferencia.Id,
            transferencia.ChaveIdempotencia,
            new PagadorComprovante(dados.PagadorId, dados.PagadorNome, dados.PagadorDocumento),
            new RecebedorComprovante(dados.RecebedorId, dados.RecebedorNome, dados.RecebedorDocumento, dados.TipoConta),
            transferencia.Valor,
            transferencia.Descricao,
            transferencia.Status,
            transferencia.CriadoEm);
    }
}
