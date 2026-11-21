using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Options;

namespace Bank.Application.Transferencias;

/// <summary>
/// Limites vigentes e uso no dia de Brasília (specs/009-limite-transferencia, FR-012). Limites e
/// disponível <c>null</c> = sem limite.
/// </summary>
public sealed record ResumoLimites(
    decimal? PorOperacao,
    decimal? Diario,
    decimal TransferidoHoje,
    decimal? DisponivelHoje,
    DateOnly Dia);

/// <summary>
/// Consulta de limites da PF (research R6): só informativa, sem transação nem bloqueio — a
/// verificação que vale é a do envio.
/// </summary>
public sealed class ConsultarLimites(
    ICarteiraConsultas carteiras,
    ITransferenciaRepositorio transferencias,
    IOptionsMonitor<LimitesTransferenciaOptions> limites,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasTransferencias metricas)
{
    public async Task<Resultado<ResumoLimites>> ExecutarAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var carteira = await carteiras.ObterDoUsuarioAsync(usuarioId, cancellationToken);
        if (carteira is null)
        {
            metricas.RegistrarConsultaLimites("usuario-nao-encontrado");
            return Resultado.Falha<ResumoLimites>(ErrosUsuario.NaoEncontrado);
        }

        var limitesPf = limites.CurrentValue.ParaPessoaFisica();
        var dia = calendario.Hoje(relogio.GetUtcNow());
        var transferidoHoje = await transferencias.SomarEnviadasAsync(carteira.CarteiraId, dia.InicioUtc, dia.FimUtc, cancellationToken);

        metricas.RegistrarConsultaLimites("consultada");
        return Resultado.Ok(new ResumoLimites(
            limitesPf.PorOperacao,
            limitesPf.Diario,
            Dinheiro.NormalizarEscala(transferidoHoje),
            limitesPf.DisponivelHoje(transferidoHoje),
            dia.Data));
    }
}
