using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Extrato paginado sobre o razão (specs/006-transferencias, research R8): índice
/// <c>ix_lancamentos_extrato</c>; contrapartes buscadas em lote para a página.
/// </summary>
internal sealed class ExtratoConsultas(BankDbContext contexto) : IExtratoConsultas
{
    public async Task<PaginaExtrato> ObterAsync(CarteiraId carteiraId, DirecaoLancamento? direcao, int deslocamento, int quantidade, CancellationToken cancellationToken)
    {
        var consulta = contexto.Lancamentos.AsNoTracking().Where(l => l.CarteiraId == carteiraId);
        if (direcao is { } filtro)
        {
            consulta = consulta.Where(l => l.Direcao == filtro);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var lancamentos = await consulta
            .OrderByDescending(l => l.CriadoEm)
            .ThenByDescending(l => l.Id)
            .Skip(deslocamento)
            .Take(quantidade)
            .ToListAsync(cancellationToken);

        var transferencias = lancamentos.Where(l => l.TransferenciaId is not null).Select(l => l.TransferenciaId!.Value).ToList();
        var contrapartes = await (
            from t in contexto.Transferencias.AsNoTracking()
            where transferencias.Contains(t.Id)
            join carteira in contexto.Carteiras on (t.CarteiraOrigemId == carteiraId ? t.CarteiraDestinoId : t.CarteiraOrigemId) equals carteira.Id
            join usuario in contexto.Usuarios on carteira.UsuarioId equals usuario.Id
            select new { t.Id, usuario.NomeCompleto, usuario.Documento, usuario.TipoConta })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var itens = lancamentos
            .Select(l => new ItemExtrato(
                l.Id,
                l.Tipo,
                l.Direcao,
                l.TransferenciaId,
                l.TransferenciaId is { } id && contrapartes.TryGetValue(id, out var c)
                    ? new ContraparteExtrato(c.NomeCompleto, c.Documento, c.TipoConta)
                    : null,
                l.Valor,
                l.Descricao,
                l.CriadoEm))
            .ToList();

        return new PaginaExtrato(itens, total);
    }
}
