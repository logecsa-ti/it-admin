namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

public record AssetDto(int Id, string AssetCode, string? SerialNumber, string Name, AssetStatus Status, int? CurrentUserId, int? LocationId, int? DepartmentId);

public record CreateAssetRequest(string AssetCode, string? SerialNumber, string Name, int AssetTypeId, string? Brand, string? Model, int? LocationId, int? DepartmentId);

public record UpdateAssetRequest(string Name, string? Description, string? Brand, string? Model, int? LocationId, int? DepartmentId);
