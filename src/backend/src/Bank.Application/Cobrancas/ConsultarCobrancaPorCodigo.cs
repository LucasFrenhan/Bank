using Bank.Application.Abstracoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Application.Cobrancas;

/// <summary>Código como chega da rota (texto).</summary>
public sealed record ConsultarCobrancaPorCodigoConsulta(UsuarioId UsuarioId, string? Codigo);

/// <summary>
/// Consulta pelo código curto, com a visibilidade da consulta por identificador
/// (specs/015-frontend-cartao-credito, FR-009b; research R3). Malformado ou inexistente: não encontrada.
/// </summary>
public sealed class ConsultarCobrancaPorCodigo(ICobrancaConsultas consultas)
{
    public async Task<Resultado<DetalheCobranca>> ExecutarAsync(ConsultarCobrancaPorCodigoConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var dados = CodigoCobranca.Criar(consulta.Codigo) is { } codigo
            ? await consultas.ObterPorCodigoAsync(codigo, cancellationToken)
            : null;
        return dados is null
            ? Resultado.Falha<DetalheCobranca>(ErrosCobranca.NaoEncontrada)
            : Resultado.Ok(DetalheCobranca.Para(consulta.UsuarioId, dados));
    }
}
