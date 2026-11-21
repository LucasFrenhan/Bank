using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.Application;

/// <summary>Banco em memória para os casos de uso de transferência.</summary>
internal sealed class BancoFalso : ICarteiraConsultas, IUsuarioConsultas, ITransferenciaRepositorio, IUnidadeDeTransferencia
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    public List<Usuario> Usuarios { get; } = [];

    public List<ExecucaoTransferencia> Gravadas { get; } = [];

    private ExecucaoTransferencia? _pendente;

    /// <summary>Simula outra requisição gravando a mesma chave enquanto esta espera o bloqueio.</summary>
    public Action? AntesDeBloquear { get; set; }

    /// <summary>Simula outra requisição gravando a mesma chave durante a confirmação.</summary>
    public Action? AntesDeConfirmar { get; set; }

    public bool ForcarChaveDuplicada { get; set; }

    public bool OmitirCarteirasNoBloqueio { get; set; }

    public int Desfeitas { get; private set; }

    public Usuario Criar(string nome, string documento, TipoConta tipo, decimal saldo)
    {
        var usuario = Usuario.Criar(nome, Email.Criar($"{Guid.NewGuid():N}@exemplo.com.br").Valor, Documento.Criar(documento, tipo).Valor, "hash", saldo, Agora);
        Usuarios.Add(usuario);
        return usuario;
    }

    public Task<DadosCarteira?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Id == usuarioId);
        return Task.FromResult(u is null ? null : new DadosCarteira(u.Carteira.Id, u.Id, u.NomeCompleto, u.TipoConta, u.Carteira.Saldo, u.Carteira.Moeda, u.Carteira.AtualizadoEm));
    }

    public Task<DadosTitular?> ObterTitularAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Id == usuarioId);
        return Task.FromResult(u is null ? null : new DadosTitular(u.Id, u.NomeCompleto, u.Email, u.Documento, u.TipoConta, Agora));
    }

    public Task<Destinatario?> ObterDestinatarioAsync(string documento, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Documento == documento);
        return Task.FromResult(u is null ? null : new Destinatario(u.Id, u.Carteira.Id, u.NomeCompleto, u.Documento, u.TipoConta));
    }

    public Task<Comprovante?> ObterComprovantePorChaveAsync(CarteiraId carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken) =>
        Comprovante(Gravadas.Find(x => x.Transferencia.CarteiraOrigemId == carteiraOrigemId && x.Transferencia.ChaveIdempotencia == chaveIdempotencia));

    /// <summary>Quantas vezes a leitura por identificador foi chamada (spec 008).</summary>
    public int LeiturasPorId { get; private set; }

    public Task<Comprovante?> ObterComprovantePorIdAsync(TransferenciaId transferenciaId, CancellationToken cancellationToken)
    {
        LeiturasPorId++;
        return Comprovante(Gravadas.Find(x => x.Transferencia.Id == transferenciaId));
    }

    /// <summary>Como o repositório real: documentos sem máscara.</summary>
    private Task<Comprovante?> Comprovante(ExecucaoTransferencia? e)
    {
        if (e is null)
        {
            return Task.FromResult<Comprovante?>(null);
        }

        var pagador = Usuarios.Single(u => u.Carteira.Id == e.Transferencia.CarteiraOrigemId);
        var recebedor = Usuarios.Single(u => u.Carteira.Id == e.Transferencia.CarteiraDestinoId);
        return Task.FromResult<Comprovante?>(new Comprovante(
            e.Transferencia.Id, e.Transferencia.ChaveIdempotencia,
            new PagadorComprovante(pagador.Id, pagador.NomeCompleto, pagador.Documento),
            new RecebedorComprovante(recebedor.Id, recebedor.NomeCompleto, recebedor.Documento, recebedor.TipoConta),
            e.Transferencia.Valor, e.Transferencia.Descricao, e.Transferencia.Status, e.Transferencia.CriadoEm));
    }

    /// <summary>Ordem das chamadas relevantes (bloqueio e soma do dia — spec 009, FR-009).</summary>
    public List<string> Chamadas { get; } = [];

    /// <summary>Já transferido hoje fora das gravadas (soma do dia — spec 009).</summary>
    public decimal TransferidoAntes { get; set; }

    /// <summary>Intervalo da última soma do dia pedida.</summary>
    public (DateTimeOffset Inicio, DateTimeOffset Fim)? UltimoIntervalo { get; private set; }

    public Task<decimal> SomarEnviadasAsync(CarteiraId carteiraOrigemId, DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken cancellationToken)
    {
        Chamadas.Add("Somar");
        UltimoIntervalo = (inicioUtc, fimUtc);
        return Task.FromResult(TransferidoAntes + Gravadas
            .Where(g => g.Transferencia.CarteiraOrigemId == carteiraOrigemId && g.Transferencia.CriadoEm >= inicioUtc && g.Transferencia.CriadoEm < fimUtc)
            .Sum(g => g.Transferencia.Valor));
    }

    /// <summary>Transações iniciadas (o limite por operação recusa antes do bloqueio — spec 009).</summary>
    public int TransacoesIniciadas { get; private set; }

    public Task IniciarAsync(CancellationToken cancellationToken)
    {
        TransacoesIniciadas++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Carteira>> BloquearCarteirasAsync(CarteiraId carteiraA, CarteiraId carteiraB, CancellationToken cancellationToken)
    {
        Chamadas.Add("Bloquear");
        AntesDeBloquear?.Invoke();
        return Task.FromResult<IReadOnlyList<Carteira>>(OmitirCarteirasNoBloqueio
            ? []
            : [.. Usuarios.Select(u => u.Carteira).Where(c => c.Id == carteiraA || c.Id == carteiraB).OrderBy(c => c.Id.Valor)]);
    }

    public void Adicionar(ExecucaoTransferencia execucao) => _pendente = execucao;

    public Task<ResultadoConfirmacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        AntesDeConfirmar?.Invoke();
        if (ForcarChaveDuplicada)
        {
            _pendente = null;
            return Task.FromResult(ResultadoConfirmacao.ChaveDuplicada);
        }

        Gravadas.Add(_pendente!);
        _pendente = null;
        return Task.FromResult(ResultadoConfirmacao.Ok);
    }

    public Task DesfazerAsync(CancellationToken cancellationToken)
    {
        Desfeitas++;
        _pendente = null;
        return Task.CompletedTask;
    }
}

/// <summary><see cref="IOptionsMonitor{TOptions}"/> fixo; o teste pode alterar a instância.</summary>
internal sealed class MonitorDeOpcoes<T>(T valor) : IOptionsMonitor<T>
{
    public T CurrentValue => valor;

    public T Get(string? name) => valor;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>Logger que guarda nível, mensagem e propriedades (auditoria da spec 009, FR-016).</summary>
internal sealed class LoggerCapturado<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensagem, IReadOnlyDictionary<string, object?> Propriedades)> Registros { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var propriedades = state is IEnumerable<KeyValuePair<string, object?>> pares
            ? pares.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        Registros.Add((logLevel, formatter(state, exception), propriedades));
    }
}
