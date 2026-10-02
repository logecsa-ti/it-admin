namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;

public class Department : AuditableSoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ManagerId { get; set; }

    public int? ParentId { get; set; }

    public bool IsActive { get; set; } = true;

    public Department? Parent { get; set; }

    public ICollection<Department>? Children { get; set; }
}

public class Location : AuditableSoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AssetType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
