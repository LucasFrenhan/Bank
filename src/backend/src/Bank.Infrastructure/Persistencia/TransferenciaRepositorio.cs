using Bank.Application.Abstracoes;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Leitura de transferências gravadas para a idempotência (specs/006-transferencias, R4).</summary>
internal sealed class TransferenciaRepositorio(BankDbContext contexto) : ITransferenciaRepositorio
{
    public async Task<Comprovante?> ObterComprovantePorChaveAsync(Guid carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken)
    {
        var dados = await (
            from t in contexto.Transferencias.AsNoTracking()
            where t.CarteiraOrigemId == carteiraOrigemId && t.ChaveIdempotencia == chaveIdempotencia
            join origem in contexto.Carteiras on t.CarteiraOrigemId equals origem.Id
            join pagador in contexto.Usuarios on origem.UsuarioId equals pagador.Id
            join destino in contexto.Carteiras on t.CarteiraDestinoId equals destino.Id
            join recebedor in contexto.Usuarios on destino.UsuarioId equals recebedor.Id
            select new
            {
                Transferencia = t,
                PagadorId = pagador.Id,
                PagadorNome = pagador.NomeCompleto,
                RecebedorId = recebedor.Id,
                RecebedorNome = recebedor.NomeCompleto,
                recebedor.Documento,
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
            new PagadorComprovante(dados.PagadorId, dados.PagadorNome),
            new RecebedorComprovante(dados.RecebedorId, dados.RecebedorNome, dados.Documento, dados.TipoConta),
            transferencia.Valor,
            transferencia.Descricao,
            transferencia.Status,
            transferencia.CriadoEm);
    }
}
