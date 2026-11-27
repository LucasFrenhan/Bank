using System.Diagnostics.Metrics;
using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas de negócio de usuários, autenticação e sessão (FR-027; research R9; specs/016-sessao-refresh-token,
/// FR-021). As tags nunca carregam dados pessoais nem tokens.
/// </summary>
public sealed class MetricasUsuarios
{
    public const string NomeMeter = "Bank.Usuarios";
    public const string NomeCadastros = "bank.usuarios.cadastros";
    public const string NomeLogins = "bank.autenticacao.logins";
    public const string NomeRenovacoes = "bank.autenticacao.renovacoes";
    public const string NomeSessoesRevogadas = "bank.autenticacao.sessoes_revogadas";
    public const string NomeSessoesRemovidas = "bank.autenticacao.sessoes_removidas";

    private readonly Counter<long> _cadastros;
    private readonly Counter<long> _logins;
    private readonly Counter<long> _renovacoes;
    private readonly Counter<long> _sessoesRevogadas;
    private readonly Counter<long> _sessoesRemovidas;

    public MetricasUsuarios(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _cadastros = meter.CreateCounter<long>(NomeCadastros, "{cadastro}", "Cadastros de usuários concluídos.");
        _logins = meter.CreateCounter<long>(NomeLogins, "{login}", "Tentativas de login por resultado.");
        _renovacoes = meter.CreateCounter<long>(NomeRenovacoes, "{renovacao}", "Renovações de sessão por resultado.");
        _sessoesRevogadas = meter.CreateCounter<long>(NomeSessoesRevogadas, "{sessao}", "Sessões revogadas por motivo.");
        _sessoesRemovidas = meter.CreateCounter<long>(NomeSessoesRemovidas, "{sessao}", "Sessões encerradas removidas pela limpeza.");
    }

    public void RegistrarCadastro(TipoConta tipoConta) =>
        _cadastros.Add(1, new KeyValuePair<string, object?>("tipo_conta", tipoConta.ToString()));

    public void RegistrarLogin(bool sucesso) =>
        _logins.Add(1, new KeyValuePair<string, object?>("resultado", sucesso ? "sucesso" : "falha"));

    /// <summary><paramref name="resultado"/>: <c>sucesso</c>, <c>recusada</c> ou <c>reuso</c>.</summary>
    public void RegistrarRenovacao(string resultado) =>
        _renovacoes.Add(1, new KeyValuePair<string, object?>("resultado", resultado));

    public void RegistrarRevogacao(MotivoRevogacao motivo) =>
        _sessoesRevogadas.Add(1, new KeyValuePair<string, object?>("motivo", Tag(motivo)));

    public void RegistrarRemocao(int quantidade)
    {
        if (quantidade > 0)
        {
            _sessoesRemovidas.Add(quantidade);
        }
    }

    public static string Tag(MotivoRevogacao motivo) => motivo switch
    {
        MotivoRevogacao.Logout => "logout",
        MotivoRevogacao.Reuso => "reuso",
        MotivoRevogacao.NovoLogin => "novo-login",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo)),
    };
}
