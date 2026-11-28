using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Cobrancas;

public sealed record CancelarCobrancaComando(UsuarioId LojistaUsuarioId, string? CobrancaId);

/// <summary>Cancela a própria cobrança pendente, com ela bloqueada (specs/014-cartao-credito-cobrancas, FR-013).</summary>
public sealed partial class CancelarCobranca(
    ICobrancaRepositorio cobrancas,
    ICobrancaConsultas consultas,
    IUnidadeDeTrabalho unidade,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<CancelarCobranca> logger)
{
    public async Task<Resultado<DetalheCobranca>> ExecutarAsync(CancelarCobrancaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var identificador = Entradas.LerIdentificador(comando.CobrancaId, ConsultarCobranca.CampoIdentificador);
        if (!identificador.Sucesso)
        {
            return Resultado.Falha<DetalheCobranca>(identificador.Erros);
        }

        var id = CobrancaId.De(identificador.Valor);
        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var cobranca = await cobrancas.BloquearAsync(id, cancellationToken);
            var resultado = cobranca is null || cobranca.LojistaUsuarioId != comando.LojistaUsuarioId
                ? Resultado.Falha(ErrosCobranca.NaoEncontrada)
                : cobranca.Cancelar(relogio.AgoraEmMicrossegundos());
            if (!resultado.Sucesso)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return Resultado.Falha<DetalheCobranca>(resultado.Erros);
            }

            await unidade.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }

        metricas.RegistrarCobrancaCancelada();
        LogCancelada(logger, id.Valor);

        var dados = await consultas.ObterAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("A cobrança cancelada não foi encontrada.");
        return Resultado.Ok(DetalheCobranca.Para(comando.LojistaUsuarioId, dados));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cobrança {CobrancaId} cancelada pelo lojista")]
    private static partial void LogCancelada(ILogger logger, Guid cobrancaId);
}
