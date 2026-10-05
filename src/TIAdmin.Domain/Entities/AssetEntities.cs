namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;

public class Asset : AuditableSoftDeletableEntity
{
    public string AssetCode { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int AssetTypeId { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public decimal? PurchaseCost { get; set; }

    public DateOnly? WarrantyExpiration { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Available;

    public int? CurrentUserId { get; set; }

    public int? LocationId { get; set; }

    public int? DepartmentId { get; set; }

    public int? VendorId { get; set; }

    public int? ParentAssetId { get; set; }

    public string? Notes { get; set; }

    public AssetType? AssetType { get; set; }

    public Location? Location { get; set; }

    public Department? Department { get; set; }

    public Asset? ParentAsset { get; set; }

    public ICollection<Asset>? Children { get; set; }
}

public class AssetAssignment : AuditableEntity
{
    public int AssetId { get; set; }

    public int UserId { get; set; }

    public DateTime AssignmentDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public int AssignedById { get; set; }

    public int? ReturnedById { get; set; }

    public string? ConditionAtAssignment { get; set; }

    public string? ConditionAtReturn { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Asset? Asset { get; set; }
}