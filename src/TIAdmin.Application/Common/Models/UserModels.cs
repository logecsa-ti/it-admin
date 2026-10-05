namespace TIAdmin.Application.Common.Models;

public record UserListItemDto(
    int Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? EmployeeCode,
    string? JobTitle,
    int? DepartmentId,
    int? LocationId,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);

public record UserDetailDto(
    int Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? EmployeeCode,
    string? JobTitle,
    int? DepartmentId,
    int? LocationId,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTimeOffset? LockoutEnd,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> DirectPermissions);

/// <summary>Filtros de <c>GET /api/v1/users</c>, ademas de <see cref="PagedQuery"/>.</summary>
public record UserFilter(bool? IsActive, int? DepartmentId, int? LocationId, string? Role);

/// <summary>Datos de perfil comunes al alta y la edicion (validados por las mismas reglas).</summary>
public interface IUserProfileData
{
    string Email { get; }

    string FirstName { get; }

    string LastName { get; }

    string? EmployeeCode { get; }

    string? JobTitle { get; }

    int? DepartmentId { get; }

    int? LocationId { get; }
}

/// <summary>
/// Alta de usuario. Se crea con el rol USER; los demas roles se asignan con
/// <c>PUT /users/{id}/roles</c>, que exige ROLES.MANAGE.
/// </summary>
public record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? EmployeeCode,
    string? JobTitle,
    int? DepartmentId,
    int? LocationId) : IUserProfileData;

public record UpdateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string? EmployeeCode,
    string? JobTitle,
    int? DepartmentId,
    int? LocationId) : IUserProfileData;

public record AssignRolesRequest(IReadOnlyList<string> Roles);

public record AssignPermissionsRequest(IReadOnlyList<string> Permissions);
