using Bank.Application.Abstracoes;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Application.Usuarios;

/// <summary>
/// Consulta dos dados cadastrais do titular autenticado (specs/005-usuarios-me, US1). Sem logs:
/// e-mail e documento não podem ir para a telemetria (FR-006).
/// </summary>
public sealed class ObterTitular(IUsuarioConsultas consultas)
{
    public async Task<Resultado<DadosTitular>> ExecutarAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var titular = await consultas.ObterTitularAsync(usuarioId, cancellationToken);

        return titular is null
            ? Resultado.Falha<DadosTitular>(ErrosUsuario.NaoEncontrado)
            : Resultado.Ok(titular);
    }
}
