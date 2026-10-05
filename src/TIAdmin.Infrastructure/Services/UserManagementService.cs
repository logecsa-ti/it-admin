namespace TIAdmin.Infrastructure.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Infrastructure.Identity;
using TIAdmin.Infrastructure.Persistence;

public sealed class UserManagementService(
    TIAdminDbContext context,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService)
    : IUserManagementService
{
    public async Task<PagedResult<UserListItemDto>> SearchAsync(
        PagedQuery query,
        UserFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var users = context.Users.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            users = users.Where(u => u.UserName!.Contains(search)
                || u.Email!.Contains(search)
                || u.FirstName.Contains(search)
                || u.LastName.Contains(search)
                || (u.EmployeeCode != null && u.EmployeeCode.Contains(search)));
        }

        if (filter.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == filter.IsActive.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            users = users.Where(u => u.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.LocationId.HasValue)
        {
            users = users.Where(u => u.LocationId == filter.LocationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var normalizedRole = filter.Role.Trim().ToUpperInvariant();
            users = users.Where(u => context.UserRoles.Any(ur => ur.UserId == u.Id
                && context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == normalizedRole)));
        }

        var descending = query.SortDirection == SortDirection.Descending;
        users = query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending
                ? users.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName)
                : users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName),
            "email" => descending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
            "lastloginat" => descending ? users.OrderByDescending(u => u.LastLoginAt) : users.OrderBy(u => u.LastLoginAt),
            _ => descending ? users.OrderByDescending(u => u.UserName) : users.OrderBy(u => u.UserName)
        };

        var total = await users.LongCountAsync(cancellationToken);
        var page = await users
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmployeeCode,
                u.JobTitle,
                u.DepartmentId,
                u.LocationId,
                u.IsActive,
                u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        var rolesByUser = await GetRolesByUserAsync(page.Select(u => u.Id).ToList(), cancellationToken);

        var items = page
            .Select(u => new UserListItemDto(
                u.Id,
                u.UserName!,
                u.Email ?? string.Empty,
                u.FirstName,
                u.LastName,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.EmployeeCode,
                u.JobTitle,
                u.DepartmentId,
                u.LocationId,
                u.IsActive,
                u.LastLoginAt,
                rolesByUser.GetValueOrDefault(u.Id, [])))
            .ToList();

        return new PagedResult<UserListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<UserDetailDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("User", id);

        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employeeCode = Normalize(request.EmployeeCode);
        await EnsureEmployeeCodeAvailableAsync(employeeCode, excludeUserId: null, cancellationToken);
        await EnsureOrganizationReferencesAsync(request.DepartmentId, request.LocationId, cancellationToken);

        var user = new ApplicationUser(request.UserName.Trim(), request.Email.Trim())
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmployeeCode = employeeCode,
            JobTitle = Normalize(request.JobTitle),
            DepartmentId = request.DepartmentId,
            LocationId = request.LocationId,
            IsActive = true
        };

        (await userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
        (await userManager.AddToRoleAsync(user, SystemRoles.User)).ThrowIfFailed();

        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await FindAsync(id);
        var employeeCode = Normalize(request.EmployeeCode);
        await EnsureEmployeeCodeAvailableAsync(employeeCode, excludeUserId: id, cancellationToken);
        await EnsureOrganizationReferencesAsync(request.DepartmentId, request.LocationId, cancellationToken);

        user.Email = request.Email.Trim();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.EmployeeCode = employeeCode;
        user.JobTitle = Normalize(request.JobTitle);
        user.DepartmentId = request.DepartmentId;
        user.LocationId = request.LocationId;

        // UpdateAsync normaliza el correo y valida que siga siendo unico.
        (await userManager.UpdateAsync(user)).ThrowIfFailed();

        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task SetActiveAsync(int id, bool isActive, int? actingUserId, CancellationToken cancellationToken = default)
    {
        if (!isActive && actingUserId == id)
        {
            throw new DomainValidationException("CANNOT_DEACTIVATE_SELF", "No puede desactivar su propio usuario.");
        }

        var user = await FindAsync(id);
        if (user.IsActive == isActive)
        {
            return;
        }

        if (!isActive && await userManager.IsInRoleAsync(user, SystemRoles.SuperAdmin))
        {
            await EnsureAnotherActiveSuperAdminAsync(id, cancellationToken);
        }

        user.IsActive = isActive;
        (await userManager.UpdateAsync(user)).ThrowIfFailed();

        if (!isActive)
        {
            // El access token vigente expira solo; sin refresh token no puede renovarse.
            await tokenService.RevokeAllForUserAsync(id, cancellationToken);
        }
    }

    public async Task<UserDetailDto> SetRolesAsync(int id, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var user = await FindAsync(id);
        var requested = roles.Select(r => r.Trim().ToUpperInvariant()).Distinct().ToList();

        var existing = await context.Roles
            .Where(r => requested.Contains(r.NormalizedName!))
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken);
        var unknown = requested.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainValidationException("ROLE_NOT_FOUND", $"Roles inexistentes: {string.Join(", ", unknown)}.");
        }

        var current = await userManager.GetRolesAsync(user);
        var toRemove = current.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = existing.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        if (user.IsActive && toRemove.Contains(SystemRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            await EnsureAnotherActiveSuperAdminAsync(id, cancellationToken);
        }

        if (toRemove.Count > 0)
        {
            (await userManager.RemoveFromRolesAsync(user, toRemove)).ThrowIfFailed();
        }

        if (toAdd.Count > 0)
        {
            (await userManager.AddToRolesAsync(user, toAdd)).ThrowIfFailed();
        }

        return await ToDetailAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> SetDirectPermissionsAsync(
        int id,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var user = await FindAsync(id);
        var desired = await context.ResolvePermissionIdsAsync(permissions, cancellationToken);

        var current = await context.UserPermissions.Where(up => up.UserId == id).ToListAsync(cancellationToken);
        context.UserPermissions.RemoveRange(current.Where(up => !desired.Contains(up.PermissionId)));
        context.UserPermissions.AddRange(desired
            .Where(pid => current.All(up => up.PermissionId != pid))
            .Select(pid => new ApplicationUserPermission { UserId = id, PermissionId = pid }));
        await context.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(user, cancellationToken);
    }

    private async Task<ApplicationUser> FindAsync(int id) =>
        await userManager.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new EntityNotFoundException("User", id);

    private async Task<UserDetailDto> ToDetailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = (await GetRolesByUserAsync([user.Id], cancellationToken)).GetValueOrDefault(user.Id, []);
        var direct = await context.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.Permission.Code)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return new UserDetailDto(
            user.Id,
            user.UserName!,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.EmployeeCode,
            user.JobTitle,
            user.DepartmentId,
            user.LocationId,
            user.IsActive,
            user.LastLoginAt,
            user.LockoutEnd,
            roles,
            direct);
    }

    private async Task<Dictionary<int, IReadOnlyList<string>>> GetRolesByUserAsync(
        List<int> userIds,
        CancellationToken cancellationToken)
    {
        var pairs = await context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, Role = r.Name! })
            .ToListAsync(cancellationToken);

        return pairs
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(p => p.Role).Order().ToList());
    }

    private async Task EnsureEmployeeCodeAvailableAsync(string? employeeCode, int? excludeUserId, CancellationToken cancellationToken)
    {
        if (employeeCode is null)
        {
            return;
        }

        var taken = await context.Users.AnyAsync(
            u => u.EmployeeCode == employeeCode && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);
        if (taken)
        {
            throw new ConflictException("EMPLOYEE_CODE_ALREADY_EXISTS", "El codigo de empleado ya esta asignado a otro usuario.");
        }
    }

    private async Task EnsureOrganizationReferencesAsync(int? departmentId, int? locationId, CancellationToken cancellationToken)
    {
        // El query filter excluye departamentos/ubicaciones eliminados.
        if (departmentId is { } dep && !await context.Departments.AnyAsync(d => d.Id == dep, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }

        if (locationId is { } loc && !await context.Locations.AnyAsync(l => l.Id == loc, cancellationToken))
        {
            throw new DomainValidationException("LOCATION_NOT_FOUND", "La ubicacion indicada no existe.");
        }
    }

    private async Task EnsureAnotherActiveSuperAdminAsync(int excludingUserId, CancellationToken cancellationToken)
    {
        var others = await context.UserRoles
            .Where(ur => ur.UserId != excludingUserId
                && context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == SystemRoles.SuperAdmin)
                && context.Users.Any(u => u.Id == ur.UserId && u.IsActive))
            .AnyAsync(cancellationToken);

        if (!others)
        {
            throw new ConflictException("LAST_SUPER_ADMIN", "Debe existir al menos un SUPER_ADMIN activo.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
