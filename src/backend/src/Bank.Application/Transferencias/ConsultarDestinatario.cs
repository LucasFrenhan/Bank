using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Transferencias;

public sealed record DestinatarioConsultado(string Nome, TipoConta TipoConta);

/// <summary>
/// Confirma o destinatário pelo documento antes do envio (spec 006, US1a; FR-009a/FR-009b):
/// devolve só nome e tipo de conta. Sem logs; documento e nome não vão para a telemetria.
/// </summary>
public sealed class ConsultarDestinatario(IUsuarioConsultas usuarios, MetricasTransferencias metricas)
{
    public const string Campo = "documento";

    public async Task<Resultado<DestinatarioConsultado>> ExecutarAsync(Guid pagadorUsuarioId, string? documento, CancellationToken cancellationToken)
    {
        var normalizado = Documento.CriarSemTipo(documento, Campo);
        if (!normalizado.Sucesso)
        {
            return Responder("invalido", Resultado.Falha<DestinatarioConsultado>(normalizado.Erros));
        }

        var destino = await usuarios.ObterDestinatarioAsync(normalizado.Valor.Valor, cancellationToken);
        if (destino is null)
        {
            return Responder("nao-encontrado", Resultado.Falha<DestinatarioConsultado>(ErrosTransferencia.DestinatarioNaoEncontrado));
        }

        return destino.UsuarioId == pagadorUsuarioId
            ? Responder("autotransferencia", Resultado.Falha<DestinatarioConsultado>(ErrosTransferencia.Autotransferencia))
            : Responder("encontrado", Resultado.Ok(new DestinatarioConsultado(destino.Nome, destino.TipoConta)));
    }

    private Resultado<DestinatarioConsultado> Responder(string resultado, Resultado<DestinatarioConsultado> resposta)
    {
        metricas.RegistrarConsultaDestinatario(resultado);
        return resposta;
    }
}
