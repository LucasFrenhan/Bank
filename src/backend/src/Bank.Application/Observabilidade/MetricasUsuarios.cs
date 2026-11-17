using System.Diagnostics.Metrics;
using Bank.Domain.Usuarios;

namespace Bank.Application.Observabilidade;

/// <summary>
/// Métricas de negócio de usuários e autenticação (FR-027; research R9). As tags nunca
/// carregam dados pessoais.
/// </summary>
public sealed class MetricasUsuarios
{
    public const string NomeMeter = "Bank.Usuarios";
    public const string NomeCadastros = "bank.usuarios.cadastros";
    public const string NomeLogins = "bank.autenticacao.logins";

    private readonly Counter<long> _cadastros;
    private readonly Counter<long> _logins;

    public MetricasUsuarios(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(NomeMeter);
        _cadastros = meter.CreateCounter<long>(NomeCadastros, "{cadastro}", "Cadastros de usuários concluídos.");
        _logins = meter.CreateCounter<long>(NomeLogins, "{login}", "Tentativas de login por resultado.");
    }

    public void RegistrarCadastro(TipoConta tipoConta) =>
        _cadastros.Add(1, new KeyValuePair<string, object?>("tipo_conta", tipoConta.ToString()));

    public void RegistrarLogin(bool sucesso) =>
        _logins.Add(1, new KeyValuePair<string, object?>("resultado", sucesso ? "sucesso" : "falha"));
}
