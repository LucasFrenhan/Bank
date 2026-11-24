using System.Collections.Frozen;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Permissoes;

/// <summary>
/// Matriz única de permissões por perfil (RN-06, FR-023; research R5):
/// PF transfere, recebe e paga cobranças de cartão; PJ recebe e gera cobranças de cartão,
/// mas não realiza transferências P2P. Cartão e faturas são da PF; recebíveis, da PJ (spec 014, R16).
/// </summary>
public static class PermissoesPorPerfil
{
    private static readonly FrozenDictionary<TipoConta, FrozenSet<Permissao>> Matriz =
        new Dictionary<TipoConta, FrozenSet<Permissao>>
        {
            [TipoConta.PessoaFisica] = new[]
            {
                Permissao.RealizarTransferencia,
                Permissao.ReceberTransferencia,
                Permissao.PagarCobrancaCartao,
                Permissao.UsarCartaoCredito,
            }.ToFrozenSet(),
            [TipoConta.PessoaJuridica] = new[]
            {
                Permissao.ReceberTransferencia,
                Permissao.GerarCobrancaCartao,
                Permissao.GerenciarRecebiveis,
            }.ToFrozenSet(),
        }.ToFrozenDictionary();

    public static bool Possui(TipoConta tipoConta, Permissao permissao) =>
        Matriz.TryGetValue(tipoConta, out var permissoes) && permissoes.Contains(permissao);

    public static IReadOnlySet<Permissao> Do(TipoConta tipoConta) =>
        Matriz.TryGetValue(tipoConta, out var permissoes) ? permissoes : FrozenSet<Permissao>.Empty;
}
