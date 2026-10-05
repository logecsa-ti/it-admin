namespace TIAdmin.Application.Common.Models;

public record RoleDto(int Id, string Name, string? Description, bool IsSystemRole, int UserCount, int PermissionCount);

public record RoleDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    IReadOnlyList<string> Permissions);

public record PermissionDto(string Code, string Module, string Action, string Description);

public record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);

/// <summary>El nombre de un rol de sistema no puede cambiar; solo su descripcion.</summary>
public record UpdateRoleRequest(string Name, string? Description);
