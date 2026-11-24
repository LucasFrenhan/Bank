using Bank.Application.Abstracoes;
using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;

namespace Bank.Application.Cobrancas;

public sealed record PagamentoDaCobranca(Bandeira Bandeira, string UltimosDigitosCartao, string CodigoAutorizacao);

public sealed record RecusaDaCobranca(MotivoRecusa Motivo, DateTimeOffset Em);

/// <summary>
/// Cobrança como cada pessoa pode vê-la (specs/014-cartao-credito-cobrancas, FR-011): o pagamento só para o
/// emissor e o pagador; a última recusa só para o emissor; a identidade do pagador nunca.
/// </summary>
public sealed record DetalheCobranca(DadosCobranca Dados, PagamentoDaCobranca? Pagamento, RecusaDaCobranca? UltimaRecusa)
{
    public static DetalheCobranca Para(UsuarioId quemConsulta, DadosCobranca dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        var ehEmissor = dados.LojistaUsuarioId == quemConsulta;
        var ehPagador = dados.PagadorUsuarioId == quemConsulta;

        return new DetalheCobranca(
            dados,
            (ehEmissor || ehPagador) && dados.CodigoAutorizacao is { } codigo
                ? new PagamentoDaCobranca(Bandeira.DeCodigo(dados.CodigoBandeira)!, dados.UltimosDigitosCartao!, codigo)
                : null,
            ehEmissor && dados.UltimaRecusaMotivo is { } motivo
                ? new RecusaDaCobranca(motivo, dados.UltimaRecusaEm!.Value)
                : null);
    }
}
