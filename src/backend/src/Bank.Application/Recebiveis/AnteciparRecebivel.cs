using Bank.Application.Abstracoes;
using Bank.Application.Comum;
using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Comum;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Recebiveis;

public sealed record AnteciparRecebivelComando(UsuarioId LojistaUsuarioId, string? RecebivelId, string? ChaveIdempotencia);

/// <summary>
/// Antecipa um recebível com taxa fixa e crédito imediato (specs/014-cartao-credito-cobrancas, US6; FR-032):
/// bloqueio recebível → carteira, crédito e lançamento numa transação.
/// </summary>
public sealed partial class AnteciparRecebivel(
    IRecebivelRepositorio recebiveis,
    IRecebivelConsultas consultas,
    ICarteiraRepositorio carteiras,
    IUnidadeDeTrabalho unidade,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<AnteciparRecebivel> logger)
{
    public async Task<Resultado<DadosRecebivel>> ExecutarAsync(AnteciparRecebivelComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var chave = Entradas.LerChaveIdempotencia(comando.ChaveIdempotencia);
        var identificador = Entradas.LerIdentificador(comando.RecebivelId, ObterRecebivel.CampoIdentificador);
        if (!chave.Sucesso || !identificador.Sucesso)
        {
            return Resultado.Falha<DadosRecebivel>([.. chave.Erros, .. identificador.Erros]);
        }

        var id = RecebivelId.De(identificador.Valor);
        var existente = await recebiveis.ObterPorChaveAsync(comando.LojistaUsuarioId, chave.Valor, cancellationToken);
        if (existente is not null)
        {
            return await ResolverExistenteAsync(existente.Value);
        }

        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var bloqueado = await recebiveis.BloquearAsync(id, cancellationToken);
            if (bloqueado is null || bloqueado.Recebivel.LojistaUsuarioId != comando.LojistaUsuarioId)
            {
                return await FalharAsync(ErrosRecebivel.NaoEncontrado);
            }

            existente = await recebiveis.ObterPorChaveAsync(comando.LojistaUsuarioId, chave.Valor, cancellationToken);
            if (existente is not null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return await ResolverExistenteAsync(existente.Value);
            }

            var recebivel = bloqueado.Recebivel;
            var agora = relogio.AgoraEmMicrossegundos();
            var antecipacao = recebivel.Antecipar(calendario.Hoje(agora).Data, chave.Valor, agora);
            if (!antecipacao.Sucesso)
            {
                return await FalharAsync(antecipacao.Erros[0]);
            }

            var carteira = await carteiras.BloquearAsync(recebivel.CarteiraId, cancellationToken)
                ?? throw new InvalidOperationException("A carteira do lojista não foi encontrada para bloqueio.");
            carteira.Creditar(recebivel.ValorLiquido, agora);
            carteiras.Adicionar(Lancamento.DeRecebivel(recebivel, bloqueado.DescricaoCobranca, agora));
            if (await unidade.ConfirmarAsync(cancellationToken) == ResultadoGravacao.ChaveDuplicada)
            {
                existente = await recebiveis.ObterPorChaveAsync(comando.LojistaUsuarioId, chave.Valor, cancellationToken);
                return existente is null
                    ? throw new InvalidOperationException("Chave de idempotência duplicada sem recebível correspondente.")
                    : await ResolverExistenteAsync(existente.Value);
            }

            metricas.RegistrarRecebivelAntecipado();
            LogAntecipado(logger, recebivel.Id.Valor, recebivel.ValorBruto, recebivel.Taxa);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }

        return await DetalharAsync();

        async Task<Resultado<DadosRecebivel>> ResolverExistenteAsync(RecebivelId antecipado) =>
            antecipado == id ? await DetalharAsync() : Resultado.Falha<DadosRecebivel>(ErrosIdempotencia.Divergente);

        async Task<Resultado<DadosRecebivel>> DetalharAsync() =>
            Resultado.Ok(await consultas.ObterAsync(comando.LojistaUsuarioId, id, cancellationToken)
                ?? throw new InvalidOperationException("O recebível antecipado não foi encontrado."));

        async Task<Resultado<DadosRecebivel>> FalharAsync(Erro erro)
        {
            await unidade.DesfazerAsync(cancellationToken);
            return Resultado.Falha<DadosRecebivel>(erro);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recebível {RecebivelId} antecipado: bruto {ValorBruto}, taxa {Taxa}")]
    private static partial void LogAntecipado(ILogger logger, Guid recebivelId, decimal valorBruto, decimal taxa);
}
