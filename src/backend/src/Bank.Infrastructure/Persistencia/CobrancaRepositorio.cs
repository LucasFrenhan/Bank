using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Mensageria;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.Infrastructure.Persistencia;

/// <summary>Cobranças e pagamento aprovado, com o evento no outbox (specs/014-cartao-credito-cobrancas, FR-018, FR-021).</summary>
internal sealed class CobrancaRepositorio(BankDbContext contexto) : ICobrancaRepositorio
{
    public const string ArquivoBloqueio = "Consultas/BloquearCobranca.sql";

    public void Adicionar(Cobranca cobranca) => contexto.Cobrancas.Add(cobranca);

    public async Task<Cobranca?> BloquearAsync(CobrancaId cobrancaId, CancellationToken cancellationToken)
    {
        var cobrancas = await contexto.Cobrancas
            .FromSqlRaw(SqlEmbutido.Ler(ArquivoBloqueio), new NpgsqlParameter("id", cobrancaId.Valor))
            .ToListAsync(cancellationToken);
        return cobrancas.SingleOrDefault();
    }

    public void Adicionar(AutorizacaoCartao autorizacao) => contexto.AutorizacoesCartao.Add(autorizacao);

    public void Adicionar(ExecucaoPagamento execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);

        contexto.AutorizacoesCartao.Add(execucao.Autorizacao);
        contexto.ComprasCartao.Add(execucao.Compra);
        contexto.Recebiveis.Add(execucao.Recebivel);
        contexto.EventosOutbox.Add(EventoOutbox.De(execucao.Evento));
    }

    public Task<PagamentoAprovado?> ObterPagamentoPorChaveAsync(UsuarioId pagador, Guid chaveIdempotencia, CancellationToken cancellationToken) =>
        (
            from autorizacao in contexto.AutorizacoesCartao.AsNoTracking()
            where autorizacao.PagadorUsuarioId == pagador
                && autorizacao.ChaveIdempotencia == chaveIdempotencia
                && autorizacao.Resultado == ResultadoAutorizacao.Aprovada
            join compra in contexto.ComprasCartao on autorizacao.Id equals compra.AutorizacaoId
            select new PagamentoAprovado(
                autorizacao.CobrancaId,
                autorizacao.Valor,
                autorizacao.CodigoBandeira!,
                autorizacao.UltimosDigitos,
                autorizacao.CodigoAutorizacao!,
                compra.Estabelecimento,
                autorizacao.CriadoEm))
            .SingleOrDefaultAsync(cancellationToken);
}
