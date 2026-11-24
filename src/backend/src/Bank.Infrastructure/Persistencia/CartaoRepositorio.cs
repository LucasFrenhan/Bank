using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Cartões (specs/014-cartao-credito-cobrancas, US1; research R4, R8).</summary>
internal sealed class CartaoRepositorio(BankDbContext contexto) : ICartaoRepositorio
{
    public const string ArquivoBloqueio = "Consultas/BloquearCartao.sql";

    public Task<Cartao?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken) =>
        contexto.Cartoes.AsNoTracking().SingleOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

    public async Task<Cartao?> BloquearAsync(CartaoId cartaoId, CancellationToken cancellationToken)
    {
        var cartoes = await contexto.Cartoes
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoBloqueio), new NpgsqlParameter("id", cartaoId.Valor))
            .ToListAsync(cancellationToken);
        return cartoes.SingleOrDefault();
    }

    public void Adicionar(Cartao cartao) => contexto.Cartoes.Add(cartao);

    public async Task<decimal> SomarComprasALancarAsync(CartaoId cartaoId, CancellationToken cancellationToken) =>
        await (
            from compra in contexto.ComprasCartao
            join fatura in contexto.Faturas on compra.FaturaId equals fatura.Id
            where compra.CartaoId == cartaoId
                && fatura.LiberadaEm == null
                && !contexto.LancamentosFatura.Any(l => l.CompraId == compra.Id)
            select (decimal?)compra.Valor)
            .SumAsync(cancellationToken) ?? 0.00m;
}
