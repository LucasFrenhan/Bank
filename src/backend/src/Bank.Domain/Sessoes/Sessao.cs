using Bank.Domain.Usuarios;

namespace Bank.Domain.Sessoes;

/// <summary>Motivo interno de uma recusa de renovação: só para log e métrica, nunca para a resposta (FR-010).</summary>
public enum MotivoRecusa
{
    Revogada,
    Inativa,
    LimiteAbsoluto,
}

/// <summary>Resultado de <see cref="Sessao.Renovar"/>.</summary>
public abstract record ResultadoRenovacao
{
    private ResultadoRenovacao()
    {
    }

    public sealed record Renovada(CredencialRenovacao Nova) : ResultadoRenovacao;

    public sealed record Recusada(MotivoRecusa Motivo) : ResultadoRenovacao;

    /// <summary>Credencial trocada há mais que a tolerância: a sessão foi revogada (FR-007).</summary>
    public sealed record ReusoDetectado : ResultadoRenovacao;
}

/// <summary>
/// Sessão iniciada por um login (raiz de agregado; specs/016-sessao-refresh-token, data-model). Concentra as regras
/// de rotação, tolerância, reuso, inatividade, limite absoluto e revogação. O repositório carrega só a credencial
/// apresentada e a ativa.
/// </summary>
public sealed class Sessao
{
    private readonly List<CredencialRenovacao> _credenciais = [];

    private Sessao()
    {
    }

    public SessaoId Id { get; private set; }

    public UsuarioId UsuarioId { get; private set; }

    public DateTimeOffset IniciadaEm { get; private set; }

    /// <summary>Login ou última renovação: base do limite de inatividade.</summary>
    public DateTimeOffset UltimaRenovacaoEm { get; private set; }

    /// <summary>Limite absoluto (<see cref="IniciadaEm"/> + <see cref="LimitesSessao.Absoluto"/>); imutável.</summary>
    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? RevogadaEm { get; private set; }

    public MotivoRevogacao? MotivoRevogacao { get; private set; }

    public IReadOnlyCollection<CredencialRenovacao> Credenciais => _credenciais;

    public bool Revogada => RevogadaEm is not null;

    /// <summary>A credencial em uso (a única não trocada entre as carregadas).</summary>
    public CredencialRenovacao CredencialAtiva => _credenciais.Single(c => c.Ativa);

    public static Sessao Iniciar(UsuarioId usuarioId, HashCredencial hash, DateTimeOffset agora, LimitesSessao limites)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(limites);

        var sessao = new Sessao
        {
            Id = SessaoId.Novo(agora),
            UsuarioId = usuarioId,
            IniciadaEm = agora,
            UltimaRenovacaoEm = agora,
            ExpiraEm = agora + limites.Absoluto,
        };
        sessao._credenciais.Add(CredencialRenovacao.Emitir(sessao.Id, hash, agora, sessao.VencimentoPorInatividade(limites)));
        return sessao;
    }

    /// <summary>
    /// Troca a credencial (FR-006): recusa se revogada, vencida pelo limite absoluto ou por inatividade; aceita a
    /// ativa ou uma trocada há até <see cref="LimitesSessao.ToleranciaReuso"/> (FR-008), sempre rotacionando a ativa;
    /// fora disso, revoga por reuso (FR-007).
    /// </summary>
    public ResultadoRenovacao Renovar(HashCredencial apresentado, HashCredencial novo, DateTimeOffset agora, LimitesSessao limites)
    {
        ArgumentNullException.ThrowIfNull(apresentado);
        ArgumentNullException.ThrowIfNull(novo);
        ArgumentNullException.ThrowIfNull(limites);

        if (Revogada)
        {
            return new ResultadoRenovacao.Recusada(MotivoRecusa.Revogada);
        }

        if (agora >= ExpiraEm)
        {
            return new ResultadoRenovacao.Recusada(MotivoRecusa.LimiteAbsoluto);
        }

        if (agora >= UltimaRenovacaoEm + limites.Inatividade)
        {
            return new ResultadoRenovacao.Recusada(MotivoRecusa.Inativa);
        }

        var credencial = _credenciais.SingleOrDefault(c => c.Hash == apresentado)
            ?? throw new InvalidOperationException("A credencial apresentada não pertence a esta sessão.");

        if (!credencial.Ativa && agora - credencial.RotacionadaEm!.Value > limites.ToleranciaReuso)
        {
            Revogar(Sessoes.MotivoRevogacao.Reuso, agora);
            return new ResultadoRenovacao.ReusoDetectado();
        }

        var ativa = CredencialAtiva;
        UltimaRenovacaoEm = agora;
        var nova = CredencialRenovacao.Emitir(Id, novo, agora, VencimentoPorInatividade(limites));
        ativa.Rotacionar(agora, nova.Id);
        _credenciais.Add(nova);
        return new ResultadoRenovacao.Renovada(nova);
    }

    /// <summary>Revoga a sessão; idempotente (a primeira revogação e o primeiro motivo valem).</summary>
    /// <returns><c>true</c> se a sessão foi revogada nesta chamada.</returns>
    public bool Revogar(MotivoRevogacao motivo, DateTimeOffset agora)
    {
        if (Revogada)
        {
            return false;
        }

        RevogadaEm = agora;
        MotivoRevogacao = motivo;
        return true;
    }

    /// <summary>Segundos até a sessão vencer sem renovação (nunca além do limite absoluto); 0 se já venceu.</summary>
    public int PrazoInatividade(DateTimeOffset agora, LimitesSessao limites) =>
        SegundosAte(VencimentoPorInatividade(limites), agora);

    /// <summary>Segundos até o limite absoluto; 0 se já passou.</summary>
    public int PrazoAbsoluto(DateTimeOffset agora) => SegundosAte(ExpiraEm, agora);

    private DateTimeOffset VencimentoPorInatividade(LimitesSessao limites)
    {
        var porInatividade = UltimaRenovacaoEm + limites.Inatividade;
        return porInatividade < ExpiraEm ? porInatividade : ExpiraEm;
    }

    private static int SegundosAte(DateTimeOffset instante, DateTimeOffset agora) =>
        instante <= agora ? 0 : (int)Math.Floor((instante - agora).TotalSeconds);
}
