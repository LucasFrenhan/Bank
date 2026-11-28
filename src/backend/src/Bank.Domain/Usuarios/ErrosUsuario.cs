using Bank.SharedKernel;

namespace Bank.Domain.Usuarios;

/// <summary>Erros de negócio de usuários e autenticação (códigos do contrato).</summary>
public static class ErrosUsuario
{
    public static readonly Erro EmailJaCadastrado =
        new("email-ja-cadastrado", "Já existe uma conta com este e-mail.", TipoErro.Conflito);

    public static readonly Erro DocumentoJaCadastrado =
        new("documento-ja-cadastrado", "Já existe uma conta com este documento.", TipoErro.Conflito);

    public static readonly Erro CredenciaisInvalidas =
        new("credenciais-invalidas", "E-mail ou senha inválidos.", TipoErro.NaoAutorizado);

    public static readonly Erro NaoEncontrado =
        new("usuario-nao-encontrado", "Usuário não encontrado para o token informado.", TipoErro.NaoEncontrado);
}
