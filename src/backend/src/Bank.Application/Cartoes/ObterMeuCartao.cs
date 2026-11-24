using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Application.Cartoes;

/// <summary>Consulta do próprio cartão, sem número completo nem CVV (specs/014-cartao-credito-cobrancas, FR-006, FR-007).</summary>
public sealed class ObterMeuCartao(ICartaoRepositorio cartoes)
{
    public async Task<Resultado<MeuCartao>> ExecutarAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var cartao = await cartoes.ObterDoUsuarioAsync(usuarioId, cancellationToken);
        if (cartao is null)
        {
            return Resultado.Falha<MeuCartao>(ErrosCartao.NaoEncontrado);
        }

        return Resultado.Ok(new MeuCartao(
            cartao,
            Bandeira.DeCodigo(cartao.CodigoBandeira)!,
            await cartoes.SomarComprasALancarAsync(cartao.Id, cancellationToken)));
    }
}

/// <summary>Dados completos para o titular; o texto do registro sai mascarado (research R2).</summary>
public sealed record DadosCompletosCartao(string NomeImpresso, NumeroCartao Numero, ValidadeCartao Validade, Cvv Cvv, Bandeira Bandeira);

/// <summary>
/// Exibe número, validade e CVV do próprio cartão, derivados na hora e nunca gravados
/// (specs/014-cartao-credito-cobrancas, FR-007, FR-008).
/// </summary>
public sealed class ExibirDadosCartao(ICartaoRepositorio cartoes, IDadosCartao dadosCartao)
{
    public async Task<Resultado<DadosCompletosCartao>> ExecutarAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var cartao = await cartoes.ObterDoUsuarioAsync(usuarioId, cancellationToken);
        if (cartao is null)
        {
            return Resultado.Falha<DadosCompletosCartao>(ErrosCartao.NaoEncontrado);
        }

        var dados = dadosCartao.Recuperar(cartao);
        return Resultado.Ok(new DadosCompletosCartao(
            cartao.NomeImpresso, dados.Numero, cartao.Validade, dados.Cvv, Bandeira.DeCodigo(cartao.CodigoBandeira)!));
    }
}
