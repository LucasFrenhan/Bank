using System.Diagnostics;
using Bank.Application.Abstracoes;
using Bank.Application.Comum;
using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Calendario;
using Bank.Domain.Cartoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Processamento;

/// <summary>O que uma rodada fez.</summary>
public sealed record ResumoProcessamento(int ComprasLancadas, int FaturasLiberadas, int RecebiveisLiquidados, int Falhas)
{
    public static readonly ResumoProcessamento Vazio = new(0, 0, 0, 0);
}

/// <summary>
/// Processamento diário (specs/014-cartao-credito-cobrancas, FR-022, FR-023; research R7): em dia útil, lança as
/// compras de dias anteriores, libera as faturas fechadas e liquida os recebíveis devidos. Seleciona por estado e
/// data, então repetir, atrasar ou rodar em várias instâncias não duplica. Cada item tem a própria transação.
/// </summary>
public sealed partial class ProcessarDia(
    ICartaoRepositorio cartoes,
    IFaturaRepositorio faturas,
    IRecebivelRepositorio recebiveis,
    ICarteiraRepositorio carteiras,
    IUnidadeDeTrabalho unidade,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasProcessamento metricas,
    ILogger<ProcessarDia> logger)
{
    public const string NomeFonte = "Bank.Processamento";
    public const string NomeSpan = "processamento diario";

    private static readonly ActivitySource Fonte = new(NomeFonte);

    public async Task<ResumoProcessamento> ExecutarAsync(CancellationToken cancellationToken)
    {
        var hoje = calendario.Hoje(relogio.GetUtcNow()).Data;
        if (!CalendarioBancario.EhDiaUtil(hoje))
        {
            return ResumoProcessamento.Vazio;
        }

        var cartoesAProcessar = await faturas.ListarCartoesAProcessarAsync(hoje, cancellationToken);
        var devidos = await recebiveis.ListarDevidosAsync(hoje, cancellationToken);
        if (cartoesAProcessar.Count == 0 && devidos.Count == 0)
        {
            return ResumoProcessamento.Vazio;
        }

        using var atividade = Fonte.StartActivity(NomeSpan);
        var inicio = relogio.GetTimestamp();
        var (lancadas, liberadas, liquidados, falhas) = (0, 0, 0, 0);

        foreach (var cartaoId in cartoesAProcessar)
        {
            try
            {
                var (compras, faturasDoCartao) = await LancarELiberarAsync(cartaoId, hoje, cancellationToken);
                lancadas += compras;
                liberadas += faturasDoCartao;
                metricas.RegistrarLancamentos(compras, faturasDoCartao);
            }
            catch (Exception excecao) when (!cancellationToken.IsCancellationRequested)
            {
                falhas++;
                RegistrarFalha(MetricasProcessamento.EtapaLancamento, cartaoId.Valor, excecao);
            }
        }

        foreach (var recebivelId in devidos)
        {
            try
            {
                if (await LiquidarAsync(recebivelId, hoje, cancellationToken))
                {
                    liquidados++;
                    metricas.RegistrarLiquidado();
                }
            }
            catch (Exception excecao) when (!cancellationToken.IsCancellationRequested)
            {
                falhas++;
                RegistrarFalha(MetricasProcessamento.EtapaLiquidacao, recebivelId.Valor, excecao);
            }
        }

        var duracao = relogio.GetElapsedTime(inicio);
        metricas.RegistrarExecucao(falhas > 0, duracao);
        LogConcluido(logger, hoje, lancadas, liberadas, liquidados, falhas, duracao.TotalMilliseconds);
        return new ResumoProcessamento(lancadas, liberadas, liquidados, falhas);
    }

    /// <summary>Lança as compras de dias anteriores e libera as faturas fechadas de um cartão, com ele bloqueado (R6).</summary>
    private async Task<(int Compras, int Faturas)> LancarELiberarAsync(CartaoId cartaoId, DateOnly hoje, CancellationToken cancellationToken)
    {
        var (lancadas, liberadas) = (0, 0);
        await unidade.IniciarAsync(cancellationToken);
        try
        {
            _ = await cartoes.BloquearAsync(cartaoId, cancellationToken)
                ?? throw new InvalidOperationException("O cartão não foi encontrado para bloqueio.");

            var agora = relogio.AgoraEmMicrossegundos();
            foreach (var (fatura, compras) in await faturas.ListarNaoLiberadasAsync(cartaoId, cancellationToken))
            {
                var aLancar = compras.Where(c => c.DataCompra < hoje).ToList();
                foreach (var compra in aLancar)
                {
                    faturas.Adicionar(fatura.Lancar(compra, agora));
                }

                lancadas += aLancar.Count;
                if (fatura.Fechamento < hoje && aLancar.Count == compras.Count && fatura.Total > 0)
                {
                    fatura.Liberar(hoje, agora);
                    liberadas++;
                }
            }

            await unidade.ConfirmarAsync(cancellationToken);
            return (lancadas, liberadas);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>Credita o recebível devido; falso se outra instância ou uma antecipação já o resolveu.</summary>
    private async Task<bool> LiquidarAsync(RecebivelId recebivelId, DateOnly hoje, CancellationToken cancellationToken)
    {
        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var agora = relogio.AgoraEmMicrossegundos();
            var bloqueado = await recebiveis.BloquearAsync(recebivelId, cancellationToken);
            if (bloqueado is null || !bloqueado.Recebivel.Liquidar(hoje, agora))
            {
                await unidade.DesfazerAsync(cancellationToken);
                return false;
            }

            var carteira = await carteiras.BloquearAsync(bloqueado.Recebivel.CarteiraId, cancellationToken)
                ?? throw new InvalidOperationException("A carteira do lojista não foi encontrada para bloqueio.");
            carteira.Creditar(bloqueado.Recebivel.ValorLiquido, agora);
            carteiras.Adicionar(Lancamento.DeRecebivel(bloqueado.Recebivel, bloqueado.DescricaoCobranca, agora));
            await unidade.ConfirmarAsync(cancellationToken);
            return true;
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    private void RegistrarFalha(string etapa, Guid itemId, Exception excecao)
    {
        metricas.RegistrarFalha(etapa);
        LogFalha(logger, excecao, etapa, itemId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processamento de {Dia}: {ComprasLancadas} compra(s) lançada(s), {FaturasLiberadas} fatura(s) liberada(s), {RecebiveisLiquidados} recebível(is) liquidado(s), {Falhas} falha(s), {DuracaoMs} ms")]
    private static partial void LogConcluido(ILogger logger, DateOnly dia, int comprasLancadas, int faturasLiberadas, int recebiveisLiquidados, int falhas, double duracaoMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha no processamento diário, etapa {Etapa}, item {ItemId}; será tentado de novo na próxima rodada")]
    private static partial void LogFalha(ILogger logger, Exception excecao, string etapa, Guid itemId);
}
