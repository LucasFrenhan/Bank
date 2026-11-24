using Bank.Application.Abstracoes;
using Bank.Application.Comum;
using Bank.Application.Observabilidade;
using Bank.Application.Transferencias;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Cobrancas;

/// <summary>Pedido de pagamento; o texto do registro não mostra os dados do cartão (research R2).</summary>
public sealed record PagarCobrancaComando(
    UsuarioId PagadorUsuarioId,
    string? CobrancaId,
    string? ChaveIdempotencia,
    string? NomeTitularCartao,
    string? NumeroCartao,
    string? Validade,
    string? Cvv)
{
    public override string ToString() => $"{nameof(PagarCobrancaComando)} {{ {nameof(CobrancaId)} = {CobrancaId} }}";
}

/// <param name="Replay">Reenvio idempotente: resultado original, sem novo consumo de limite.</param>
public sealed record CobrancaPagaComCartao(PagamentoAprovado Pagamento, bool Replay);

/// <summary>
/// Pagamento de cobrança com o cartão One Bank do próprio pagador (specs/014-cartao-credito-cobrancas, US3;
/// FR-014 a FR-021): validação, idempotência (research R10), bloqueio cobrança → cartão (R8), decisão da
/// bandeira (R11) e gravação atômica. Nenhum saldo muda.
/// </summary>
public sealed partial class PagarCobranca(
    ICobrancaRepositorio cobrancas,
    ICartaoRepositorio cartoes,
    IFaturaRepositorio faturas,
    IUsuarioConsultas usuarios,
    IDadosCartao dadosCartao,
    IUnidadeDeTrabalho unidade,
    CalendarioBrasilia calendario,
    TimeProvider relogio,
    MetricasCartoes metricas,
    ILogger<PagarCobranca> logger)
{
    public const string CampoTitular = "nomeTitularCartao";

    public async Task<Resultado<CobrancaPagaComCartao>> ExecutarAsync(PagarCobrancaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var validacao = Validar(comando);
        if (!validacao.Sucesso)
        {
            return Resultado.Falha<CobrancaPagaComCartao>(validacao.Erros);
        }

        var entrada = validacao.Valor;
        var cartaoDoPagador = await cartoes.ObterDoUsuarioAsync(comando.PagadorUsuarioId, cancellationToken);

        // Os dados do cartão não mudam depois da emissão: a conferência pode ficar fora da transação.
        var dadosConferem = cartaoDoPagador is not null
            && dadosCartao.Conferem(cartaoDoPagador, entrada.Numero, entrada.Validade, entrada.Cvv);

        var existente = await cobrancas.ObterPagamentoPorChaveAsync(comando.PagadorUsuarioId, entrada.Chave, cancellationToken);
        if (existente is not null)
        {
            return ResolverExistente(existente, entrada.CobrancaId, dadosConferem);
        }

        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var cobranca = await cobrancas.BloquearAsync(entrada.CobrancaId, cancellationToken);
            if (cobranca is null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return Resultado.Falha<CobrancaPagaComCartao>(ErrosCobranca.NaoEncontrada);
            }

            var cartao = dadosConferem ? await cartoes.BloquearAsync(cartaoDoPagador!.Id, cancellationToken) : null;

            // Requisições simultâneas com a mesma chave esperam nos bloqueios (R10).
            existente = await cobrancas.ObterPagamentoPorChaveAsync(comando.PagadorUsuarioId, entrada.Chave, cancellationToken);
            if (existente is not null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return ResolverExistente(existente, entrada.CobrancaId, dadosConferem);
            }

            if (cobranca.Status != StatusCobranca.Pendente)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return Resultado.Falha<CobrancaPagaComCartao>(ErrosCobranca.NaoPendente);
            }

            // Relógio lido com o cartão bloqueado: a compra fica no dia certo mesmo na virada (R6).
            var agora = relogio.AgoraEmMicrossegundos();
            var hoje = calendario.Hoje(agora).Data;

            var temVencida = cartao is not null && await faturas.ExisteVencidaAsync(cartao.Id, hoje, cancellationToken);
            if (AutorizadorBandeira.Decidir(cartao is not null, temVencida, cartao?.LimiteDisponivel ?? 0m, cobranca.Valor) is { } motivo)
            {
                return await RecusarAsync(cobranca, comando.PagadorUsuarioId, cartao?.Id, entrada, motivo, agora, cancellationToken);
            }

            var lojista = await usuarios.ObterTitularAsync(cobranca.LojistaUsuarioId, cancellationToken)
                ?? throw new InvalidOperationException("O lojista da cobrança não foi encontrado.");
            var (fatura, dataCompra) = await ObterFaturaAsync(cartao!.Id, hoje, agora, cancellationToken);
            var execucao = PagamentoCobranca.Executar(cobranca, cartao, fatura, lojista.NomeCompleto, dataCompra, entrada.Chave, agora);
            if (!execucao.Sucesso)
            {
                throw new InvalidOperationException("A bandeira aprovou um pagamento que o domínio recusou.");
            }

            cobrancas.Adicionar(execucao.Valor);
            if (await unidade.ConfirmarAsync(cancellationToken) == ResultadoGravacao.ChaveDuplicada)
            {
                existente = await cobrancas.ObterPagamentoPorChaveAsync(comando.PagadorUsuarioId, entrada.Chave, cancellationToken);
                return existente is null
                    ? throw new InvalidOperationException("Chave de idempotência duplicada sem pagamento correspondente.")
                    : ResolverExistente(existente, entrada.CobrancaId, dadosConferem);
            }

            var autorizacao = execucao.Valor.Autorizacao;
            metricas.RegistrarAutorizacao(autorizacao);
            LogAprovada(logger, cobranca.Id.Valor, cartao.Id.Valor, cobranca.Valor);

            return Resultado.Ok(new CobrancaPagaComCartao(
                new PagamentoAprovado(
                    cobranca.Id, cobranca.Valor, autorizacao.CodigoBandeira!, autorizacao.UltimosDigitos,
                    autorizacao.CodigoAutorizacao!, lojista.NomeCompleto, agora),
                Replay: false));
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Resultado<CobrancaPagaComCartao>> RecusarAsync(
        Cobranca cobranca, UsuarioId pagador, CartaoId? cartaoId, Entrada entrada, MotivoRecusa motivo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var recusa = AutorizacaoCartao.Recusar(cobranca, pagador, cartaoId, entrada.Numero, motivo, entrada.Chave, agora);
        cobranca.RegistrarRecusa(recusa);
        cobrancas.Adicionar(recusa);
        await unidade.ConfirmarAsync(cancellationToken);

        metricas.RegistrarAutorizacao(recusa);
        LogRecusada(logger, cobranca.Id.Valor, motivo);
        return Resultado.Falha<CobrancaPagaComCartao>(AutorizadorBandeira.ErroDe(motivo));
    }

    /// <summary>
    /// Fatura do ciclo da compra. Se ela já foi liberada (relógio desta instância atrasado), a compra vai para o
    /// primeiro dia do ciclo seguinte (research R6).
    /// </summary>
    private async Task<(Fatura Fatura, DateOnly DataCompra)> ObterFaturaAsync(CartaoId cartaoId, DateOnly hoje, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var dataCompra = hoje;
        while (true)
        {
            var competencia = CompetenciaFatura.De(dataCompra);
            var fatura = await faturas.ObterOuAbrirAsync(cartaoId, competencia, agora, cancellationToken);
            if (fatura.LiberadaEm is null)
            {
                return (fatura, dataCompra);
            }

            dataCompra = competencia.Fechamento.AddDays(1);
        }
    }

    private static Resultado<CobrancaPagaComCartao> ResolverExistente(PagamentoAprovado existente, CobrancaId cobrancaId, bool dadosConferem) =>
        existente.CobrancaId == cobrancaId && dadosConferem
            ? Resultado.Ok(new CobrancaPagaComCartao(existente, Replay: true))
            : Resultado.Falha<CobrancaPagaComCartao>(ErrosIdempotencia.Divergente);

    /// <summary>Acumula os erros de todos os campos (FR-015).</summary>
    private Resultado<Entrada> Validar(PagarCobrancaComando comando)
    {
        var chave = Entradas.LerChaveIdempotencia(comando.ChaveIdempotencia);
        var id = Entradas.LerIdentificador(comando.CobrancaId, ConsultarCobranca.CampoIdentificador);
        var numero = NumeroCartao.Criar(comando.NumeroCartao);
        var validade = ValidadeCartao.Criar(comando.Validade);
        var cvv = Domain.Cartoes.Cvv.Criar(comando.Cvv);

        List<Erro> erros = [.. chave.Erros, .. id.Erros];
        if (string.IsNullOrWhiteSpace(comando.NomeTitularCartao))
        {
            erros.Add(Erro.Validacao(CampoTitular, "Informe o nome do titular do cartão."));
        }

        erros.AddRange(numero.Erros);
        erros.AddRange(validade.Erros);
        if (validade.Sucesso && validade.Valor.EstaVencida(calendario.Hoje(relogio.GetUtcNow()).Data))
        {
            erros.Add(Erro.Validacao(ValidadeCartao.Campo, "O cartão está vencido."));
        }

        erros.AddRange(cvv.Erros);

        return erros.Count > 0
            ? Resultado.Falha<Entrada>(erros)
            : Resultado.Ok(new Entrada(chave.Valor, CobrancaId.De(id.Valor), numero.Valor, validade.Valor, cvv.Valor));
    }

    private sealed record Entrada(Guid Chave, CobrancaId CobrancaId, NumeroCartao Numero, ValidadeCartao Validade, Cvv Cvv);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cobrança {CobrancaId} paga com o cartão {CartaoId}: valor {Valor}")]
    private static partial void LogAprovada(ILogger logger, Guid cobrancaId, Guid cartaoId, decimal valor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pagamento da cobrança {CobrancaId} recusado pela bandeira: {Motivo}")]
    private static partial void LogRecusada(ILogger logger, Guid cobrancaId, MotivoRecusa motivo);
}
