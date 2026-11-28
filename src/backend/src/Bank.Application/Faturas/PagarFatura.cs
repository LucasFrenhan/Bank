using Bank.Application.Abstracoes;
using Bank.Application.Cartoes;
using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Cartoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Faturas;

public sealed record PagarFaturaComando(UsuarioId UsuarioId, string? Competencia, string? ChaveIdempotencia);

/// <summary>
/// Pagamento total da fatura com o saldo da carteira (specs/014-cartao-credito-cobrancas, US5; FR-028, FR-028a):
/// bloqueio cartão → carteira, juros do dia, débito, lançamento e liberação do limite numa transação.
/// </summary>
public sealed partial class PagarFatura(
    ICartaoRepositorio cartoes,
    IFaturaRepositorio faturas,
    ICarteiraConsultas carteirasConsultas,
    ICarteiraRepositorio carteiras,
    IUnidadeDeTrabalho unidade,
    ObterFatura obterFatura,
    IOptionsMonitor<CartoesOptions> opcoes,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<PagarFatura> logger)
{
    public async Task<Resultado<DetalheFatura>> ExecutarAsync(PagarFaturaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var chave = Entradas.LerChaveIdempotencia(comando.ChaveIdempotencia);
        var competencia = CompetenciaFatura.Criar(comando.Competencia);
        if (!chave.Sucesso || !competencia.Sucesso)
        {
            return Resultado.Falha<DetalheFatura>([.. chave.Erros, .. competencia.Erros]);
        }

        var cartaoDoUsuario = await cartoes.ObterDoUsuarioAsync(comando.UsuarioId, cancellationToken);
        if (cartaoDoUsuario is null)
        {
            return Resultado.Falha<DetalheFatura>(ErrosCartao.NaoEncontrado);
        }

        var carteiraDoUsuario = await carteirasConsultas.ObterDoUsuarioAsync(comando.UsuarioId, cancellationToken);
        if (carteiraDoUsuario is null)
        {
            return Resultado.Falha<DetalheFatura>(ErrosUsuario.NaoEncontrado);
        }

        var existente = await faturas.ObterCompetenciaPorChaveAsync(cartaoDoUsuario.Id, chave.Valor, cancellationToken);
        if (existente is not null)
        {
            return await ResolverExistenteAsync(existente.Value);
        }

        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var cartao = await cartoes.BloquearAsync(cartaoDoUsuario.Id, cancellationToken)
                ?? throw new InvalidOperationException("O cartão não foi encontrado para bloqueio.");
            existente = await faturas.ObterCompetenciaPorChaveAsync(cartao.Id, chave.Valor, cancellationToken);
            if (existente is not null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return await ResolverExistenteAsync(existente.Value);
            }

            var agora = relogio.AgoraEmMicrossegundos();
            var hoje = calendario.Hoje(agora).Data;
            var fatura = await faturas.ObterAsync(cartao.Id, competencia.Valor, cancellationToken);
            if (fatura is null)
            {
                // O ciclo atual sem compras existe (aberto), mas não tem o que pagar.
                return await FalharAsync(competencia.Valor == CompetenciaFatura.De(hoje) ? ErrosFatura.NaoDisponivel : ErrosFatura.NaoEncontrada);
            }

            var pagamento = fatura.Pagar(hoje, opcoes.CurrentValue.JurosAtrasoMensalPercentual, chave.Valor, agora);
            if (!pagamento.Sucesso)
            {
                return await FalharAsync(pagamento.Erros[0]);
            }

            var carteira = await carteiras.BloquearAsync(carteiraDoUsuario.CarteiraId, cancellationToken)
                ?? throw new InvalidOperationException("A carteira não foi encontrada para bloqueio.");
            if (!carteira.Debitar(pagamento.Valor.ValorAPagar, agora).Sucesso)
            {
                return await FalharAsync(ErrosFatura.SaldoInsuficiente);
            }

            cartao.LiberarLimite(pagamento.Valor.Total);
            carteiras.Adicionar(Lancamento.DePagamentoFatura(carteira.Id, fatura, agora));
            if (await unidade.ConfirmarAsync(cancellationToken) == ResultadoGravacao.ChaveDuplicada)
            {
                existente = await faturas.ObterCompetenciaPorChaveAsync(cartao.Id, chave.Valor, cancellationToken);
                return existente is null
                    ? throw new InvalidOperationException("Chave de idempotência duplicada sem fatura correspondente.")
                    : await ResolverExistenteAsync(existente.Value);
            }

            metricas.RegistrarFaturaPaga(pagamento.Valor.DiasAtraso > 0);
            LogPaga(logger, fatura.Id.Valor, cartao.Id.Valor, pagamento.Valor.ValorAPagar, pagamento.Valor.DiasAtraso);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }

        return await obterFatura.DetalharAsync(cartaoDoUsuario.Id, competencia.Valor, cancellationToken);

        async Task<Resultado<DetalheFatura>> ResolverExistenteAsync(CompetenciaFatura paga) =>
            paga == competencia.Valor
                ? await obterFatura.DetalharAsync(cartaoDoUsuario.Id, paga, cancellationToken)
                : Resultado.Falha<DetalheFatura>(ErrosIdempotencia.Divergente);

        async Task<Resultado<DetalheFatura>> FalharAsync(Erro erro)
        {
            await unidade.DesfazerAsync(cancellationToken);
            return Resultado.Falha<DetalheFatura>(erro);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fatura {FaturaId} do cartão {CartaoId} paga: valor {Valor}, {DiasAtraso} dia(s) de atraso")]
    private static partial void LogPaga(ILogger logger, Guid faturaId, Guid cartaoId, decimal valor, int diasAtraso);
}
