using Bank.SharedKernel;

namespace Bank.Domain.Sessoes;

/// <summary>Erro único das recusas de renovação, sem o motivo (specs/016-sessao-refresh-token, FR-010).</summary>
public static class ErrosSessao
{
    public const string CodigoNaoAutenticado = "nao-autenticado";

    public static readonly Erro NaoAutenticado =
        new(CodigoNaoAutenticado, "Sessão inválida ou expirada. Entre novamente.", TipoErro.NaoAutorizado);
}
