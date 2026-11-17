using Bank.Domain.Comum;

namespace Bank.Domain.Carteiras;

/// <summary>Erros de negócio de carteiras (códigos do contrato).</summary>
public static class ErrosCarteira
{
    public static readonly Erro NaoEncontrada =
        new("carteira-nao-encontrada", "Carteira não encontrada para o usuário autenticado.", TipoErro.NaoEncontrado);
}
