namespace TIAdmin.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Infrastructure.Services;

/// <summary>
/// Autoriza por permiso granular (SPECS.md seccion 16) en lugar de por rol.
/// Esto permite crear roles nuevos sin modificar codigo.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var permissions = context.User
            .FindAll(TiClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // SUPER_ADMIN es el escape hatch para administracion de la plataforma.
        if (permissions.Contains(Permissions.ConfigurationManage)
            && context.User.IsInRole(SystemRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
