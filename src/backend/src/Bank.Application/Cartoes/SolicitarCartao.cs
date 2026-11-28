using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Cartoes;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Cartoes;

public sealed record SolicitarCartaoComando(UsuarioId UsuarioId, string? Bandeira);

/// <summary>Cartão e o total das compras ainda não lançadas em fatura (FR-007).</summary>
public sealed record MeuCartao(Cartao Cartao, Bandeira Bandeira, decimal ComprasALancar);

/// <summary>
/// Emite o cartão da PF com a bandeira escolhida (specs/014-cartao-credito-cobrancas, US1; FR-002 a FR-005). O
/// número derivado que já existir muda de variação e tenta de novo (research R1).
/// </summary>
public sealed partial class SolicitarCartao(
    ICartaoRepositorio cartoes,
    IUsuarioConsultas usuarios,
    IDadosCartao dadosCartao,
    IUnidadeDeTrabalho unidade,
    IOptionsMonitor<CartoesOptions> opcoes,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<SolicitarCartao> logger)
{
    public const string CampoBandeira = "bandeira";

    public async Task<Resultado<MeuCartao>> ExecutarAsync(SolicitarCartaoComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (Bandeira.DeCodigo(comando.Bandeira) is not { } bandeira)
        {
            return Resultado.Falha<MeuCartao>(Erro.Validacao(CampoBandeira, "Escolha a bandeira: 001 (Visa) ou 002 (Mastercard)."));
        }

        if (await cartoes.ObterDoUsuarioAsync(comando.UsuarioId, cancellationToken) is not null)
        {
            return Resultado.Falha<MeuCartao>(ErrosCartao.JaExistente);
        }

        var titular = await usuarios.ObterTitularAsync(comando.UsuarioId, cancellationToken);
        if (titular is null)
        {
            return Resultado.Falha<MeuCartao>(ErrosUsuario.NaoEncontrado);
        }

        var agora = relogio.AgoraEmMicrossegundos();
        var validade = ValidadeCartao.DaEmissao(calendario.Hoje(agora).Data);
        var id = CartaoId.Novo(agora);

        for (var variacao = 0; variacao <= Cartao.MaximoVariacao; variacao++)
        {
            var dados = dadosCartao.Derivar(id, bandeira, variacao, validade);
            var cartao = Cartao.Emitir(
                id, comando.UsuarioId, bandeira, titular.NomeCompleto, validade, variacao,
                dados.Numero.UltimosDigitos, dados.ImpressaoDigital, opcoes.CurrentValue.LimitePadrao, agora);

            await unidade.IniciarAsync(cancellationToken);
            cartoes.Adicionar(cartao);
            switch (await ConfirmarAsync(cancellationToken))
            {
                case ResultadoGravacao.Ok:
                    metricas.RegistrarEmitido(bandeira.Codigo);
                    LogEmitido(logger, cartao.Id.Valor, bandeira.Codigo);
                    return Resultado.Ok(new MeuCartao(cartao, bandeira, 0.00m));
                case ResultadoGravacao.CartaoDoUsuarioDuplicado:
                    return Resultado.Falha<MeuCartao>(ErrosCartao.JaExistente);
                default:
                    continue; // número já usado: próxima variação
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um número de cartão inédito.");
    }

    private async Task<ResultadoGravacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await unidade.ConfirmarAsync(cancellationToken);
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cartão {CartaoId} emitido com a bandeira {Bandeira}")]
    private static partial void LogEmitido(ILogger logger, Guid cartaoId, string bandeira);
}
