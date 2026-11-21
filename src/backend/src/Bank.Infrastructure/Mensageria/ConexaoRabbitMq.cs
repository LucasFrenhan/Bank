using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Conexão única do processo com o broker, criada sob demanda (specs/010-mensageria-rabbitmq,
/// research R3). Sem a recuperação automática do RabbitMQ.Client: o processo é dono da recuperação.
/// Uma conexão fechada está morta, e a próxima chamada a descarta e abre outra; despachante e
/// consumidores reabrem as próprias sessões (<see cref="SessaoMensageria"/>).
/// </summary>
internal sealed partial class ConexaoRabbitMq(IOptions<MensageriaOptions> opcoes, ILogger<ConexaoRabbitMq> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;

    public bool Aberta => _conexao is { IsOpen: true };

    public async Task<IConnection> ObterAsync(CancellationToken cancellationToken)
    {
        if (_conexao is { IsOpen: true } aberta)
        {
            return aberta;
        }

        await _trava.WaitAsync(cancellationToken);
        try
        {
            if (_conexao is { IsOpen: true } abertaNaEspera)
            {
                return abertaNaEspera;
            }

            if (_conexao is not null)
            {
                var morta = _conexao;
                _conexao = null;
                await morta.DisposeAsync();
            }

            var configuracao = opcoes.Value;
            var fabrica = new ConnectionFactory
            {
                HostName = configuracao.Host,
                Port = configuracao.Porta,
                VirtualHost = configuracao.VirtualHost,
                UserName = configuracao.Usuario,
                Password = configuracao.Senha,
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                ClientProvidedName = "bank-worker",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                ContinuationTimeout = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(10),
            };

            var nova = await fabrica.CreateConnectionAsync(cancellationToken);
            nova.ConnectionShutdownAsync += AoFecharAsync;
            _conexao = nova;
            return nova;
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is not null)
        {
            await _conexao.DisposeAsync();
        }

        _trava.Dispose();
    }

    /// <summary>Só o fechamento que o processo não pediu (broker parado, conexão derrubada, rede).</summary>
    private Task AoFecharAsync(object? remetente, ShutdownEventArgs argumentos)
    {
        if (argumentos.Initiator != ShutdownInitiator.Application)
        {
            LogConexaoFechada(logger, argumentos.Initiator.ToString(), argumentos.ReplyCode, argumentos.ReplyText);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 5019, EventName = "ConexaoBrokerFechada", Level = LogLevel.Warning,
        Message = "Conexão com o broker fechada ({Iniciador} {Codigo}): {Motivo}")]
    private static partial void LogConexaoFechada(ILogger logger, string iniciador, ushort codigo, string motivo);
}
