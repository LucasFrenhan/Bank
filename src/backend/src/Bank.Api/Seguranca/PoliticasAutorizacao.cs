using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bank.Api.Seguranca;

/// <summary>
/// Políticas <c>Permissao:&lt;nome&gt;</c> para os endpoints de transferência e cobrança das
/// próximas specs (FR-023; contracts/usuarios-carteiras-api.md).
/// </summary>
public static class PoliticasAutorizacao
{
    public const string Prefixo = "Permissao:";

    public static string Nome(Permissao permissao) => Prefixo + permissao;

    internal static void AdicionarPoliticasDePermissao(AuthorizationBuilder autorizacao)
    {
        autorizacao.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, PermissaoRequirementHandler>());

        foreach (var permissao in Enum.GetValues<Permissao>())
        {
            autorizacao.AddPolicy(Nome(permissao), politica => politica
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissaoRequirement(permissao)));
        }
    }
}
