namespace TIAdmin.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Infrastructure.Persistence;

public sealed class PermissionService(TIAdminDbContext context) : IPermissionService
{
    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Union de permisos por rol + permisos directos (SPECS.md 15.2).
        // Distinct evita duplicados cuando un usuario tiene varios roles con el mismo permiso.
        // Se usan joins explicitos porque IdentityUserRole<int> no expone navegaciones.
        var roleIds = context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId);

        var permissions = await context.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .Concat(context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Code))
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    public async Task<IReadOnlyCollection<string>> GetRolesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(context.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => role.Name!)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthenticatedUser?> GetUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new AuthenticatedUser(
            user.Id,
            user.UserName!,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            await GetRolesAsync(userId, cancellationToken),
            await GetPermissionsAsync(userId, cancellationToken));
    }
}
