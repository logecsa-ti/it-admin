namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;

/// <summary>
/// Administracion de usuarios. La implementa Infrastructure sobre ASP.NET Core Identity,
/// que Application no conoce. Los errores de negocio se lanzan como DomainException
/// (404/400/409 via ExceptionHandlingMiddleware).
/// </summary>
/// <remarks>
/// Los permisos viajan como claims en el JWT: los cambios de roles/permisos aplican
/// cuando el usuario renueva su access token.
/// </remarks>
public interface IUserManagementService
{
    Task<PagedResult<UserListItemDto>> SearchAsync(PagedQuery query, UserFilter filter, CancellationToken cancellationToken = default);

    Task<UserDetailDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserDetailDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Desactiva el usuario y revoca sus refresh tokens. No se permite sobre uno mismo.</summary>
    Task SetActiveAsync(int id, bool isActive, int? actingUserId, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza los roles. Garantiza que siempre quede al menos un SUPER_ADMIN activo.</summary>
    Task<UserDetailDto> SetRolesAsync(int id, IReadOnlyList<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza los permisos directos (adicionales a los de sus roles).</summary>
    Task<UserDetailDto> SetDirectPermissionsAsync(int id, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);
}

/// <summary>
/// Administracion de roles y su matriz de permisos (RBAC dinamico, SPECS.md seccion 16).
/// </summary>
public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RoleDetailDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);

    Task<RoleDetailDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleDetailDto> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza los permisos del rol. Los de SUPER_ADMIN son inmutables.</summary>
    Task<RoleDetailDto> SetPermissionsAsync(int id, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>Solo roles que no son de sistema y sin usuarios asignados.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
