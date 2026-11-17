namespace Bank.Infrastructure.Persistencia;

/// <summary>Configuração de persistência (research R7).</summary>
public sealed class PersistenciaOptions
{
    public const string Secao = "Persistencia";
    public const string NomeConnectionString = "Bank";

    /// <summary>
    /// Aplica as migrations ao iniciar. Ligado só em <c>Development</c> e <c>Testing</c>;
    /// produção usa script/bundle (spec de deploy).
    /// </summary>
    public bool AplicarMigracoesNaInicializacao { get; set; }
}
