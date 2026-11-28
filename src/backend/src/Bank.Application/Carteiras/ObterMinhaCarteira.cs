using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Application.Carteiras;

/// <summary>Caso de uso de consulta da própria carteira (US3, FR-022).</summary>
public sealed class ObterMinhaCarteira(ICarteiraConsultas consultas)
{
    public async Task<Resultado<DadosCarteira>> ExecutarAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var carteira = await consultas.ObterDoUsuarioAsync(usuarioId, cancellationToken);

        return carteira is null
            ? Resultado.Falha<DadosCarteira>(ErrosCarteira.NaoEncontrada)
            : Resultado.Ok(carteira with { Saldo = Dinheiro.NormalizarEscala(carteira.Saldo) });
    }
}
