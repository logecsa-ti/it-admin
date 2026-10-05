namespace TIAdmin.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Infrastructure.Persistence;

public sealed class UserDirectory(TIAdminDbContext context) : IUserDirectory
{
    public async Task<UserReference?> FindAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserReference(u.Id, u.UserName!, (u.FirstName + " " + u.LastName).Trim(), u.IsActive, u.Email))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserReference>> FindActiveAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => new UserReference(u.Id, u.UserName!, (u.FirstName + " " + u.LastName).Trim(), u.IsActive, u.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> FindActiveWithPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var permissionId = context.Permissions.Where(p => p.Code == permission).Select(p => p.Id);
        var viaRoles = context.UserRoles
            .Where(ur => context.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && permissionId.Contains(rp.PermissionId)))
            .Select(ur => ur.UserId);
        var direct = context.UserPermissions
            .Where(up => permissionId.Contains(up.PermissionId))
            .Select(up => up.UserId);

        return await context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (viaRoles.Contains(u.Id) || direct.Contains(u.Id)))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
