using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Laço de sessões (specs/010-mensageria-rabbitmq, research R3): roda a sessão até ela terminar
/// (perda de canal ou conexão) ou falhar, e abre outra após 0 s, 1 s, 2 s, 4 s, 8 s e depois 10 s
/// fixos. A sessão chama <c>marcarEstabelecida</c> quando está pronta, o que zera a espera.
/// </summary>
internal static partial class SessaoMensageria
{
    private static readonly TimeSpan[] Esperas =
        [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10)];

    public static async Task ExecutarAsync(
        string nome,
        Func<Action, CancellationToken, Task> sessao,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(logger);

        var falhasSeguidas = 0;
        long? perdidaEm = null;
        var jaEstabelecida = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var estabelecida = false;
            try
            {
                await sessao(() =>
                {
                    estabelecida = true;
                    if (jaEstabelecida && perdidaEm is { } inicio)
                    {
                        var foraMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
                        LogSessaoReaberta(logger, nome, foraMs);
                    }

                    jaEstabelecida = true;
                    perdidaEm = null;
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                LogSessaoPerdida(logger, nome, "sessão encerrada");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Toda falha da sessão leva a uma nova tentativa; o processo não pode cair.
            catch (Exception excecao)
#pragma warning restore CA1031
            {
                if (estabelecida)
                {
                    LogSessaoPerdida(logger, nome, $"{excecao.GetType().Name}: {excecao.Message}");
                }
                else
                {
                    LogFalhaAbertura(logger, nome, $"{excecao.GetType().Name}: {excecao.Message}", Esperas[Math.Min(falhasSeguidas + 1, Esperas.Length - 1)].TotalMilliseconds);
                }
            }

            perdidaEm ??= Stopwatch.GetTimestamp();
            falhasSeguidas = estabelecida ? 0 : falhasSeguidas + 1;

            try
            {
                await Task.Delay(Esperas[Math.Min(falhasSeguidas, Esperas.Length - 1)], cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(EventId = 5016, EventName = "SessaoPerdida", Level = LogLevel.Warning,
        Message = "Sessão de mensageria {Sessao} perdida: {Motivo}")]
    private static partial void LogSessaoPerdida(ILogger logger, string sessao, string motivo);

    [LoggerMessage(EventId = 5017, EventName = "FalhaAberturaSessao", Level = LogLevel.Warning,
        Message = "Falha ao abrir a sessão de mensageria {Sessao}: {Motivo}. Próxima tentativa em {EsperaMs} ms")]
    private static partial void LogFalhaAbertura(ILogger logger, string sessao, string motivo, double esperaMs);

    [LoggerMessage(EventId = 5018, EventName = "SessaoReaberta", Level = LogLevel.Information,
        Message = "Sessão de mensageria {Sessao} reaberta após {ForaMs} ms")]
    private static partial void LogSessaoReaberta(ILogger logger, string sessao, long foraMs);
}
