using Bank.SharedKernel;

namespace Bank.Domain.Recebiveis;

/// <summary>Erros de recebível (specs/014-cartao-credito-cobrancas, FR-036).</summary>
public static class ErrosRecebivel
{
    public static readonly Erro NaoEncontrado =
        new("recebivel-nao-encontrado", "Recebível não encontrado.", TipoErro.NaoEncontrado);

    public static readonly Erro NaoAntecipavel =
        new("recebivel-nao-antecipavel", "Este recebível não pode mais ser antecipado.", TipoErro.Conflito);
}
