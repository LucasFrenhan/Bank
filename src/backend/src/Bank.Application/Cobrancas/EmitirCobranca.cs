using Bank.Application.Abstracoes;
using Bank.Application.Comum;
using Bank.Application.Observabilidade;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Cobrancas;

public sealed record EmitirCobrancaComando(UsuarioId LojistaUsuarioId, decimal? Valor, string? Descricao);

/// <summary>Emite uma cobrança na carteira do lojista (specs/014-cartao-credito-cobrancas, US2; FR-009).</summary>
public sealed partial class EmitirCobranca(
    ICobrancaRepositorio cobrancas,
    ICobrancaConsultas consultas,
    ICarteiraConsultas carteiras,
    IUnidadeDeTrabalho unidade,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<EmitirCobranca> logger)
{
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

        var cobranca = Cobranca.Emitir(comando.LojistaUsuarioId, carteira.CarteiraId, valor.Valor, descricao.Valor, relogio.AgoraEmMicrossegundos());
        await unidade.IniciarAsync(cancellationToken);
        try
        {
            cobrancas.Adicionar(cobranca);
            await unidade.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }

        metricas.RegistrarCobrancaEmitida();
        LogEmitida(logger, cobranca.Id.Valor, comando.LojistaUsuarioId.Valor, cobranca.Valor);

        var dados = await consultas.ObterAsync(cobranca.Id, cancellationToken)
            ?? throw new InvalidOperationException("A cobrança emitida não foi encontrada.");
        return Resultado.Ok(DetalheCobranca.Para(comando.LojistaUsuarioId, dados));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cobrança {CobrancaId} emitida pelo lojista {LojistaUsuarioId}: valor {Valor}")]
    private static partial void LogEmitida(ILogger logger, Guid cobrancaId, Guid lojistaUsuarioId, decimal valor);
}
