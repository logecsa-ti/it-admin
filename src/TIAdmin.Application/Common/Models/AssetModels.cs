namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

public record AssetListItemDto(
    int Id,
    string AssetCode,
    string? SerialNumber,
    string Name,
    int AssetTypeId,
    string AssetTypeName,
    string? Brand,
    string? Model,
    AssetStatus Status,
    int? CurrentUserId,
    string? CurrentUserName,
    int? LocationId,
    string? LocationName,
    int? DepartmentId,
    string? DepartmentName,
    DateOnly? WarrantyExpiration);

public record AssetDetailDto(
    int Id,
    string AssetCode,
    string? SerialNumber,
    string Name,
    string? Description,
    int AssetTypeId,
    string AssetTypeName,
    string? Brand,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiration,
    AssetStatus Status,
    int? LocationId,
    string? LocationName,
    int? DepartmentId,
    string? DepartmentName,
    int? ParentAssetId,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    AssetAssignmentDto? CurrentAssignment);

public record AssetAssignmentDto(
    int Id,
    int AssetId,
    string AssetCode,
    string AssetName,
    int UserId,
    string UserName,
    DateTime AssignmentDate,
    DateTime? ReturnDate,
    int AssignedById,
    string AssignedByName,
    int? ReturnedById,
    string? ReturnedByName,
    string? ConditionAtAssignment,
    string? ConditionAtReturn,
    string? Notes,
    bool IsActive);

public record AssetMovementDto(
    int Id,
    AssetMovementType MovementType,
    string? FromValue,
    string? ToValue,
    int? UserId,
    string? UserName,
    string? Notes,
    DateTime Timestamp);

public record AssetTypeDto(int Id, string Code, string Name, string? Description, bool IsActive);

/// <summary>Filtros de <c>GET /api/v1/assets</c>, ademas de <see cref="PagedQuery"/>.</summary>
public record AssetFilter(
    AssetStatus? Status,
    int? AssetTypeId,
    int? DepartmentId,
    int? LocationId,
    int? CurrentUserId,
    DateOnly? WarrantyExpiresBefore);

/// <summary>Filtros de <c>GET /api/v1/assignments</c>.</summary>
public record AssignmentFilter(int? UserId, int? AssetId, bool? ActiveOnly);

/// <summary>Campos editables comunes al alta y la edicion (validados por las mismas reglas).</summary>
public interface IAssetData
{
    string? SerialNumber { get; }

    string Name { get; }

    string? Description { get; }

    int AssetTypeId { get; }

    string? Brand { get; }

    string? Model { get; }

    DateOnly? PurchaseDate { get; }

    decimal? PurchaseCost { get; }

    DateOnly? WarrantyExpiration { get; }

    int? LocationId { get; }

    int? DepartmentId { get; }

    int? ParentAssetId { get; }

    string? Notes { get; }
}

/// <summary>El activo nace en estado Available. AssetCode es la etiqueta fisica: no cambia despues.</summary>
public record CreateAssetRequest(
    string AssetCode,
    string? SerialNumber,
    string Name,
    string? Description,
    int AssetTypeId,
    string? Brand,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiration,
    int? LocationId,
    int? DepartmentId,
    int? ParentAssetId,
    string? Notes) : IAssetData;

/// <summary>El estado no se edita aqui: usar /status, /assign o /return.</summary>
public record UpdateAssetRequest(
    string? SerialNumber,
    string Name,
    string? Description,
    int AssetTypeId,
    string? Brand,
    string? Model,
    DateOnly? PurchaseDate,
    decimal? PurchaseCost,
    DateOnly? WarrantyExpiration,
    int? LocationId,
    int? DepartmentId,
    int? ParentAssetId,
    string? Notes) : IAssetData;

public record ChangeAssetStatusRequest(AssetStatus Status, string? Notes);

public record AssignAssetRequest(int UserId, string? Condition, string? Notes);

/// <summary><paramref name="ResultingStatus"/>: Available (por defecto), Maintenance o Repair.</summary>
public record ReturnAssetRequest(string? Condition, string? Notes, AssetStatus? ResultingStatus);

public record CreateAssetTypeRequest(string Code, string Name, string? Description);

public record UpdateAssetTypeRequest(string Name, string? Description, bool IsActive);
