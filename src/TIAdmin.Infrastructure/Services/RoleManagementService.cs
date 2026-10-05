namespace TIAdmin.Infrastructure.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Infrastructure.Identity;
using TIAdmin.Infrastructure.Persistence;

public sealed class RoleManagementService(
    TIAdminDbContext context,
    RoleManager<ApplicationRole> roleManager)
    : IRoleManagementService
{
    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id,
                r.Name!,
                r.Description,
                r.IsSystemRole,
                context.UserRoles.Count(ur => ur.RoleId == r.Id),
                r.Permissions.Count))
            .ToListAsync(cancellationToken);

    public async Task<RoleDetailDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await ToDetailAsync(await FindAsync(id), cancellationToken);

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default) =>
        await context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Action)
            .Select(p => new PermissionDto(p.Code, p.Module, p.Action, p.Description))
            .ToListAsync(cancellationToken);

    public async Task<RoleDetailDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var permissionIds = await context.ResolvePermissionIdsAsync(request.Permissions ?? [], cancellationToken);

        var role = new ApplicationRole(request.Name.Trim(), Normalize(request.Description)) { IsSystemRole = false };
        (await roleManager.CreateAsync(role)).ThrowIfFailed();

        if (permissionIds.Count > 0)
        {
            context.RolePermissions.AddRange(permissionIds.Select(pid =>
                new ApplicationRolePermission { RoleId = role.Id, PermissionId = pid }));
            await context.SaveChangesAsync(cancellationToken);
        }

        return await ToDetailAsync(role, cancellationToken);
    }

    public async Task<RoleDetailDto> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = await FindAsync(id);
        var name = request.Name.Trim();

        if (!string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            if (role.IsSystemRole)
            {
                throw new DomainValidationException("SYSTEM_ROLE_RENAME", "No se puede renombrar un rol de sistema.");
            }

            (await roleManager.SetRoleNameAsync(role, name)).ThrowIfFailed();
        }

        role.Description = Normalize(request.Description);
        (await roleManager.UpdateAsync(role)).ThrowIfFailed();

        return await ToDetailAsync(role, cancellationToken);
    }

    public async Task<RoleDetailDto> SetPermissionsAsync(
        int id,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var role = await FindAsync(id);
        if (string.Equals(role.Name, SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("SUPER_ADMIN_IMMUTABLE",
                "Los permisos de SUPER_ADMIN no se pueden modificar: siempre incluye todos.");
        }

        var desired = await context.ResolvePermissionIdsAsync(permissions, cancellationToken);
        var current = await context.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync(cancellationToken);

        context.RolePermissions.RemoveRange(current.Where(rp => !desired.Contains(rp.PermissionId)));
        context.RolePermissions.AddRange(desired
            .Where(pid => current.All(rp => rp.PermissionId != pid))
            .Select(pid => new ApplicationRolePermission { RoleId = id, PermissionId = pid }));
        await context.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(role, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(id);

        if (role.IsSystemRole)
        {
            throw new ConflictException("SYSTEM_ROLE_DELETE", "No se puede eliminar un rol de sistema.");
        }

        if (await context.UserRoles.AnyAsync(ur => ur.RoleId == id, cancellationToken))
        {
            throw new ConflictException("ROLE_IN_USE", "No se puede eliminar un rol con usuarios asignados.");
        }

        // RolePermissions se elimina en cascada; el AuditLog conserva el registro del rol.
        (await roleManager.DeleteAsync(role)).ThrowIfFailed();
    }

    private async Task<ApplicationRole> FindAsync(int id) =>
        await roleManager.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new EntityNotFoundException("Role", id);

    private async Task<RoleDetailDto> ToDetailAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        var permissions = await context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission.Code)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
        var userCount = await context.UserRoles.CountAsync(ur => ur.RoleId == role.Id, cancellationToken);

        return new RoleDetailDto(role.Id, role.Name!, role.Description, role.IsSystemRole, userCount, permissions);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
