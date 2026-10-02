namespace TIAdmin.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using TIAdmin.Domain.Enums;

/// <summary>
/// Usuario de la aplicacion. Hereda de IdentityUser para aprovechar el schema
/// de ASP.NET Core Identity (hashing, lockout, normalizacion).
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? EmployeeCode { get; set; }

    public string? JobTitle { get; set; }

    public int? DepartmentId { get; set; }

    public int? LocationId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public string? ExternalProvider { get; set; }

    public string? ExternalId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public ICollection<ApplicationUserPermission> Permissions { get; set; } = [];

    public ApplicationUser()
    {
    }

    public ApplicationUser(string userName, string email)
        : base(userName)
    {
        Email = email;
        UserName = userName;
    }
}

/// <summary>
/// Rol de la aplicacion.
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public ApplicationRole()
    {
    }

    public ApplicationRole(string name, string? description = null)
        : base(name)
    {
        Description = description;
        NormalizedName = name.ToUpperInvariant();
    }

    public ICollection<ApplicationRolePermission> Permissions { get; set; } = [];
}

/// <summary>
/// Permiso granular e independiente de los roles (SPECS.md seccion 16).
/// </summary>
public class ApplicationPermission
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ICollection<ApplicationRolePermission> RolePermissions { get; set; } = [];

    public ICollection<ApplicationUserPermission> UserPermissions { get; set; } = [];
}

/// <summary>
/// Union rol-permiso.
/// </summary>
public class ApplicationRolePermission
{
    public int RoleId { get; set; }

    public ApplicationRole Role { get; set; } = null!;

    public int PermissionId { get; set; }

    public ApplicationPermission Permission { get; set; } = null!;
}

/// <summary>
/// Permiso asignado directamente a un usuario, adicionalmente a sus roles
/// (SPECS.md seccion 15.2).
/// </summary>
public class ApplicationUserPermission
{
    public int UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public int PermissionId { get; set; }

    public ApplicationPermission Permission { get; set; } = null!;
}
