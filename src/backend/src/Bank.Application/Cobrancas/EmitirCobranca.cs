using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Cobrancas;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Cobrancas;

public sealed record EmitirCobrancaComando(UsuarioId LojistaUsuarioId, decimal? Valor, string? Descricao);

/// <summary>
/// Emite uma cobrança na carteira do lojista (specs/014-cartao-credito-cobrancas, US2; FR-009). O código sorteado que já
/// existir é trocado (specs/015-frontend-cartao-credito, research R1).
/// </summary>
public sealed partial class EmitirCobranca(
    ICobrancaRepositorio cobrancas,
    ICobrancaConsultas consultas,
    ICarteiraConsultas carteiras,
    IUnidadeDeTrabalho unidade,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<EmitirCobranca> logger)
{
    public const int MaximoTentativas = 5;

    public async Task<Resultado<DetalheCobranca>> ExecutarAsync(EmitirCobrancaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var valor = ValorTransferencia.Criar(comando.Valor);
        var descricao = Cobranca.ValidarDescricao(comando.Descricao);
        if (!valor.Sucesso || !descricao.Sucesso)
        {
            return Resultado.Falha<DetalheCobranca>([.. valor.Erros, .. descricao.Erros]);
        }

        var carteira = await carteiras.ObterDoUsuarioAsync(comando.LojistaUsuarioId, cancellationToken);
        if (carteira is null)
        {
            return Resultado.Falha<DetalheCobranca>(ErrosUsuario.NaoEncontrado);
        }

        for (var tentativa = 1; tentativa <= MaximoTentativas; tentativa++)
        {
            var cobranca = Cobranca.Emitir(
                comando.LojistaUsuarioId, carteira.CarteiraId, valor.Valor, descricao.Valor, CodigoCobranca.Novo(), relogio.AgoraEmMicrossegundos());
            if (!await GravarAsync(cobranca, cancellationToken))
            {
                continue;
            }

            metricas.RegistrarCobrancaEmitida();
            LogEmitida(logger, cobranca.Id.Valor, comando.LojistaUsuarioId.Valor, cobranca.Valor);

            var dados = await consultas.ObterAsync(cobranca.Id, cancellationToken)
                ?? throw new InvalidOperationException("A cobrança emitida não foi encontrada.");
            return Resultado.Ok(DetalheCobranca.Para(comando.LojistaUsuarioId, dados));
        }

        throw new InvalidOperationException("Não foi possível gerar um código de cobrança inédito.");
    }

    /// <summary>Falso se o código sorteado já existe.</summary>
    private async Task<bool> GravarAsync(Cobranca cobranca, CancellationToken cancellationToken)
    {
        await unidade.IniciarAsync(cancellationToken);
        try
        {
            cobrancas.Adicionar(cobranca);
            return await unidade.ConfirmarAsync(cancellationToken) != ResultadoGravacao.CodigoDeCobrancaDuplicado;
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cobrança {CobrancaId} emitida pelo lojista {LojistaUsuarioId}: valor {Valor}")]
    private static partial void LogEmitida(ILogger logger, Guid cobrancaId, Guid lojistaUsuarioId, decimal valor);
}
