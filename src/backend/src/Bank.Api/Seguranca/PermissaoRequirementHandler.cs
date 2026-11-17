using Bank.Domain.Permissoes;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authorization;

namespace Bank.Api.Seguranca;

/// <summary>Exige uma <see cref="Permissao"/> do perfil indicado pela claim <c>tipo_conta</c>.</summary>
public sealed class PermissaoRequirement(Permissao permissao) : IAuthorizationRequirement
{
    public Permissao Permissao { get; } = permissao;
}

/// <summary>Consulta a matriz do domínio (<see cref="PermissoesPorPerfil"/>) — research R5.</summary>
public sealed class PermissaoRequirementHandler : AuthorizationHandler<PermissaoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissaoRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var tipoConta = TiposConta.Converter(context.User.FindFirst(ClaimsToken.TipoConta)?.Value);

        if (tipoConta.Sucesso && PermissoesPorPerfil.Possui(tipoConta.Valor, requirement.Permissao))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
