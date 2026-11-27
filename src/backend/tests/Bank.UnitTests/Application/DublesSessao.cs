using System.Security.Cryptography;
using System.Text;
using Bank.Application.Abstracoes;
using Bank.Application.Autenticacao;
using Bank.Domain.Sessoes;
using Microsoft.Extensions.Options;

namespace Bank.UnitTests.Application;

/// <summary>Sessões em memória (specs/016-sessao-refresh-token): a busca acha a sessão por qualquer credencial dela.</summary>
internal sealed class SessoesFalsas : ISessaoRepositorio, ILimpezaSessoes
{
    public List<Sessao> Sessoes { get; } = [];

    public int Buscas { get; private set; }

    public (DateTimeOffset Limite, TimeSpan Inatividade)? UltimaLimpeza { get; private set; }

    public int RemocoesSimuladas { get; set; }

    public Task<Sessao?> ObterPorCredencialComBloqueioAsync(HashCredencial hash, CancellationToken cancellationToken)
    {
        Buscas++;
        return Task.FromResult(Sessoes.Find(s => s.Credenciais.Any(c => c.Hash == hash)));
    }

    public void Adicionar(Sessao sessao) => Sessoes.Add(sessao);

    public Task<int> RemoverEncerradasAntesDeAsync(DateTimeOffset limite, TimeSpan inatividade, CancellationToken cancellationToken)
    {
        UltimaLimpeza = (limite, inatividade);
        return Task.FromResult(RemocoesSimuladas);
    }
}

/// <summary>Gerador previsível: <c>cred-1</c>, <c>cred-2</c>…; o hash é o SHA-256 do valor, como o real.</summary>
internal sealed class GeradorFalso : IGeradorCredencial
{
    private int _contador;

    public CredencialGerada Gerar()
    {
        var valor = "cred-" + Interlocked.Increment(ref _contador);
        return new CredencialGerada(valor, CalcularHash(valor));
    }

    public HashCredencial CalcularHash(string valor) => Hash(valor);

    public static HashCredencial Hash(string valor) => HashCredencial.De(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}

/// <summary>Transação observável: conta inícios, confirmações e desfazimentos.</summary>
internal sealed class UnidadeFalsa : IUnidadeDeTrabalho
{
    public int Inicios { get; private set; }

    public int Confirmacoes { get; private set; }

    public int Desfeitas { get; private set; }

    public Task IniciarAsync(CancellationToken cancellationToken)
    {
        Inicios++;
        return Task.CompletedTask;
    }

    public Task<ResultadoGravacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        Confirmacoes++;
        return Task.FromResult(ResultadoGravacao.Ok);
    }

    public Task DesfazerAsync(CancellationToken cancellationToken)
    {
        Desfeitas++;
        return Task.CompletedTask;
    }
}

/// <summary>Relógio que os testes avançam.</summary>
internal sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Avancar(TimeSpan intervalo) => Agora += intervalo;
}

internal static class OpcoesSessao
{
    public static SessaoOptions Padrao() => new();

    public static IOptionsMonitor<SessaoOptions> Monitor(SessaoOptions? opcoes = null) => new MonitorFixo(opcoes ?? Padrao());

    private sealed class MonitorFixo(SessaoOptions valor) : IOptionsMonitor<SessaoOptions>
    {
        public SessaoOptions CurrentValue => valor;

        public SessaoOptions Get(string? name) => valor;

        public IDisposable? OnChange(Action<SessaoOptions, string?> listener) => null;
    }
}
