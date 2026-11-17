using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Repositório em memória para os testes dos casos de uso.</summary>
internal sealed class RepositorioFalso : IUsuarioRepositorio
{
    public List<Usuario> Usuarios { get; } = [];

    /// <summary>Simula a violação de unicidade da base (cadastro concorrente).</summary>
    public ResultadoPersistencia? ResultadoForcado { get; set; }

    public int Atualizacoes { get; private set; }

    public Task<bool> ExisteEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.Exists(u => u.Email == email));

    public Task<bool> ExisteDocumentoAsync(string documento, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.Exists(u => u.Documento == documento));

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Usuarios.Find(u => u.Email == email));

    public Task<ResultadoPersistencia> AdicionarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        if (ResultadoForcado is { } forcado)
        {
            return Task.FromResult(forcado);
        }

        Usuarios.Add(usuario);
        return Task.FromResult(ResultadoPersistencia.Ok);
    }

    public Task AtualizarAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        Atualizacoes++;
        return Task.CompletedTask;
    }
}

/// <summary>Hasher reversível e observável: o hash é <c>hash:{senha}</c>.</summary>
internal sealed class HasherFalso : IHasherSenha
{
    public List<string> HashesVerificados { get; } = [];

    public ResultadoVerificacaoSenha? ResultadoForcado { get; set; }

    public string GerarHash(string senha) => "hash:" + senha;

    public ResultadoVerificacaoSenha Verificar(string hash, string senha)
    {
        HashesVerificados.Add(hash);
        return ResultadoForcado ?? (hash == "hash:" + senha ? ResultadoVerificacaoSenha.Sucesso : ResultadoVerificacaoSenha.Falha);
    }
}

internal sealed class EmissorFalso : IEmissorToken
{
    public TokenEmitido Emitir(Usuario usuario) => new("token-de:" + usuario.Id, 3600);
}

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}

/// <summary>Cria <see cref="MetricasUsuarios"/> e captura as medições publicadas.</summary>
internal sealed class MetricasCapturadas : IDisposable
{
    private readonly MeterFactorySimples _factory = new();
    private readonly MeterListener _listener = new();

    public MetricasCapturadas()
    {
        Metricas = new MetricasUsuarios(_factory);
        _listener.InstrumentPublished = (instrumento, listener) =>
        {
            if (_factory.Meters.Contains(instrumento.Meter))
            {
                listener.EnableMeasurementEvents(instrumento);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrumento, valor, tags, _) =>
            Medicoes.Add((instrumento.Name, valor, tags.ToArray().ToDictionary(t => t.Key, t => t.Value?.ToString()))));
        _listener.Start();
    }

    public MetricasUsuarios Metricas { get; }

    public ConcurrentBag<(string Nome, long Valor, Dictionary<string, string?> Tags)> Medicoes { get; } = [];

    public void Dispose()
    {
        _listener.Dispose();
        _factory.Dispose();
    }

    private sealed class MeterFactorySimples : IMeterFactory
    {
        public List<Meter> Meters { get; } = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            Meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in Meters)
            {
                meter.Dispose();
            }
        }
    }
}
