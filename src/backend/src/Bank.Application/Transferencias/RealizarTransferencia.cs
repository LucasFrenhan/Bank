using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Transferencias;

/// <summary>Pedido de transferência; campos opcionais para que ausências virem erros por campo.</summary>
public sealed record RealizarTransferenciaComando(
    Guid PagadorUsuarioId,
    string? ChaveIdempotencia,
    string? DocumentoDestino,
    decimal? Valor,
    string? Descricao);

/// <param name="Replay">Reenvio idempotente: comprovante original, nada foi debitado de novo.</param>
public sealed record TransferenciaRealizada(Comprovante Comprovante, bool Replay);

/// <summary>
/// Caso de uso de transferência (specs/006-transferencias US1–US3): validação, idempotência por
/// pagador (R4), bloqueio ordenado das carteiras e gravação atômica (R3). As regras de saldo e
/// autotransferência ficam no domínio.
/// </summary>
public sealed partial class RealizarTransferencia(
    ICarteiraConsultas carteiras,
    IUsuarioConsultas usuarios,
    ITransferenciaRepositorio transferencias,
    IUnidadeDeTransferencia unidade,
    TimeProvider relogio,
    MetricasTransferencias metricas,
    ILogger<RealizarTransferencia> logger)
{
    public const string CampoChave = "Idempotency-Key";
    public const string CampoDocumento = "documentoDestino";

    public async Task<Resultado<TransferenciaRealizada>> ExecutarAsync(RealizarTransferenciaComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var validacao = Validar(comando);
        if (!validacao.Sucesso)
        {
            return Recusar(validacao.Erros);
        }

        var (chave, documento, valor, descricao) = validacao.Valor;

        var pagador = await carteiras.ObterDoUsuarioAsync(comando.PagadorUsuarioId, cancellationToken);
        if (pagador is null)
        {
            return Recusar([ErrosUsuario.NaoEncontrado]);
        }

        // Idempotência antes de qualquer bloqueio (R4, passo 1).
        var existente = await transferencias.ObterComprovantePorChaveAsync(pagador.CarteiraId, chave, cancellationToken);
        if (existente is not null)
        {
            return ResolverExistente(existente, documento, valor);
        }

        var destino = await usuarios.ObterDestinatarioAsync(documento.Valor, cancellationToken);
        if (destino is null)
        {
            return Recusar([ErrosTransferencia.DestinatarioNaoEncontrado]);
        }

        if (destino.CarteiraId == pagador.CarteiraId)
        {
            return Recusar([ErrosTransferencia.Autotransferencia]);
        }

        // Documento do pagador para o comprovante (spec 008, FR-002a), lido antes da transação para
        // não alongar o bloqueio das carteiras.
        var titular = await usuarios.ObterTitularAsync(comando.PagadorUsuarioId, cancellationToken);
        if (titular is null)
        {
            return Recusar([ErrosUsuario.NaoEncontrado]);
        }

        await unidade.IniciarAsync(cancellationToken);
        try
        {
            var bloqueadas = await unidade.BloquearCarteirasAsync(pagador.CarteiraId, destino.CarteiraId, cancellationToken);
            var origem = bloqueadas.FirstOrDefault(c => c.Id == pagador.CarteiraId);
            var carteiraDestino = bloqueadas.FirstOrDefault(c => c.Id == destino.CarteiraId);
            if (origem is null || carteiraDestino is null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return Recusar([ErrosTransferencia.DestinatarioNaoEncontrado]);
            }

            // Requisições simultâneas com a mesma chave esperam no bloqueio da origem (R4, passo 2).
            existente = await transferencias.ObterComprovantePorChaveAsync(pagador.CarteiraId, chave, cancellationToken);
            if (existente is not null)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return ResolverExistente(existente, documento, valor);
            }

            var agora = relogio.GetUtcNow();
            var execucao = Transferencia.Executar(origem, carteiraDestino, valor, descricao, chave, agora);
            if (!execucao.Sucesso)
            {
                await unidade.DesfazerAsync(cancellationToken);
                return Recusar(execucao.Erros);
            }

            unidade.Adicionar(execucao.Valor);
            if (await unidade.ConfirmarAsync(cancellationToken) == ResultadoConfirmacao.ChaveDuplicada)
            {
                // Rede de segurança (R4, passo 3): outra requisição gravou a mesma chave.
                existente = await transferencias.ObterComprovantePorChaveAsync(pagador.CarteiraId, chave, cancellationToken);
                return existente is null
                    ? throw new InvalidOperationException("Chave de idempotência duplicada sem transferência correspondente.")
                    : ResolverExistente(existente, documento, valor);
            }

            var transferencia = execucao.Valor.Transferencia;
            metricas.RegistrarConcluida(destino.TipoConta, transferencia.Valor);
            LogConcluida(logger, transferencia.Id, transferencia.CarteiraOrigemId, transferencia.CarteiraDestinoId);

            return Resultado.Ok(new TransferenciaRealizada(
                new Comprovante(
                    transferencia.Id,
                    chave,
                    new PagadorComprovante(comando.PagadorUsuarioId, pagador.NomeTitular, Documento.Ocultar(titular.Documento)),
                    new RecebedorComprovante(destino.UsuarioId, destino.Nome, destino.Documento, destino.TipoConta),
                    transferencia.Valor,
                    transferencia.Descricao,
                    transferencia.Status,
                    transferencia.CriadoEm),
                Replay: false));
        }
        catch
        {
            await unidade.DesfazerAsync(CancellationToken.None);
            throw;
        }
    }

    private Resultado<TransferenciaRealizada> ResolverExistente(Comprovante existente, Documento documento, ValorTransferencia valor)
    {
        // Mesmos destinatário e valor = mesma requisição (a descrição não entra, como na #13).
        if (existente.Recebedor.Documento == documento.Valor && existente.Valor == valor.Valor)
        {
            metricas.RegistrarReplay();
            var comPagadorMascarado = existente with { Pagador = existente.Pagador with { Documento = Documento.Ocultar(existente.Pagador.Documento) } };
            return Resultado.Ok(new TransferenciaRealizada(comPagadorMascarado, Replay: true));
        }

        return Recusar([ErrosTransferencia.IdempotenciaDivergente]);
    }

    private Resultado<TransferenciaRealizada> Recusar(IReadOnlyList<Erro> erros)
    {
        metricas.RegistrarRecusa(erros[0].Codigo);
        return Resultado.Falha<TransferenciaRealizada>(erros);
    }

    /// <summary>Acumula os erros de todos os campos (400 com todos de uma vez).</summary>
    private static Resultado<(Guid Chave, Documento Documento, ValorTransferencia Valor, string? Descricao)> Validar(RealizarTransferenciaComando comando)
    {
        var erros = new List<Erro>();

        if (!Guid.TryParse(comando.ChaveIdempotencia, out var chave) || chave == Guid.Empty)
        {
            erros.Add(Erro.Validacao(CampoChave, "Informe no cabeçalho Idempotency-Key um UUID que identifique esta transferência."));
        }

        var documento = Documento.CriarSemTipo(comando.DocumentoDestino, CampoDocumento);
        erros.AddRange(documento.Erros);

        var valor = ValorTransferencia.Criar(comando.Valor);
        erros.AddRange(valor.Erros);

        var descricao = DescricaoTransferencia.Normalizar(comando.Descricao);
        erros.AddRange(descricao.Erros);

        return erros.Count > 0
            ? Resultado.Falha<(Guid, Documento, ValorTransferencia, string?)>(erros)
            : Resultado.Ok((chave, documento.Valor, valor.Valor, descricao.Valor));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Transferência {TransferenciaId} concluída da carteira {CarteiraOrigemId} para {CarteiraDestinoId}")]
    private static partial void LogConcluida(ILogger logger, Guid transferenciaId, Guid carteiraOrigemId, Guid carteiraDestinoId);
}
