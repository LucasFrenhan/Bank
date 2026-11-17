namespace Bank.Application.Abstracoes;

public enum ResultadoVerificacaoSenha
{
    Falha,
    Sucesso,

    /// <summary>Senha correta, mas o hash usa parâmetros antigos e deve ser regravado.</summary>
    SucessoRehashNecessario,
}

/// <summary>Hash seguro de senha com sal (FR-010; research R3).</summary>
public interface IHasherSenha
{
    string GerarHash(string senha);

    ResultadoVerificacaoSenha Verificar(string hash, string senha);
}
