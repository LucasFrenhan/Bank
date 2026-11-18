using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Application.Transferencias;

/// <summary>Identificador como chega da rota (texto); inválido vira erro no campo.</summary>
public sealed record ConsultarTransferenciaConsulta(Guid UsuarioId, string? TransferenciaId);

/// <summary>
/// Reabre o comprovante de uma transferência para o pagador ou o recebedor
/// (specs/008-consulta-transferencia: US1–US3; data-model "Regra de acesso"). Inexistente e
/// "não participa" seguem o mesmo caminho e recebem o mesmo erro (FR-004).
/// </summary>
public sealed class ConsultarTransferencia(ITransferenciaRepositorio transferencias, MetricasTransferencias metricas)
{
    public const string CampoIdentificador = "transferenciaId";

    public async Task<Resultado<Comprovante>> ExecutarAsync(ConsultarTransferenciaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (!Guid.TryParse(consulta.TransferenciaId, out var transferenciaId) || transferenciaId == Guid.Empty)
        {
            metricas.RegistrarConsulta("invalida");
            return Resultado.Falha<Comprovante>(Erro.Validacao(CampoIdentificador, "Informe um identificador de transferência válido."));
        }

        var comprovante = await transferencias.ObterComprovantePorIdAsync(transferenciaId, cancellationToken);
        var ehPagador = comprovante?.Pagador.UsuarioId == consulta.UsuarioId;
        var ehRecebedor = comprovante?.Recebedor.UsuarioId == consulta.UsuarioId;

        if (comprovante is null || (!ehPagador && !ehRecebedor))
        {
            metricas.RegistrarConsulta("nao-encontrada");
            return Resultado.Falha<Comprovante>(ErrosTransferencia.TransferenciaNaoEncontrada);
        }

        metricas.RegistrarConsulta("encontrada");
        return Resultado.Ok(comprovante with
        {
            // A chave é um dado do envio do pagador (FR-003).
            ChaveIdempotencia = ehPagador ? comprovante.ChaveIdempotencia : null,
            Pagador = comprovante.Pagador with { Documento = Documento.Ocultar(comprovante.Pagador.Documento) },
        });
    }
}
