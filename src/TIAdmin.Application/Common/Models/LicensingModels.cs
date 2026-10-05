namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

public record SoftwareDto(
    int Id,
    string Name,
    string? Version,
    string? Publisher,
    string? Category,
    string? Description,
    bool IsActive,
    int LicenseCount,
    int TotalSeats,
    int UsedSeats);

public record SoftwareFilter(bool? IsActive, string? Category);

public record SoftwareRequest(
    string Name,
    string? Version,
    string? Publisher,
    string? Category,
    string? Description,
    bool IsActive);

/// <summary>Nunca incluye la clave: <paramref name="HasLicenseKey"/> indica si existe (ver GET /licenses/{id}/key).</summary>
public record LicenseDto(
    int Id,
    int SoftwareId,
    string SoftwareName,
    int? VendorId,
    string? VendorName,
    int? ContractId,
    string? ContractNumber,
    string Name,
    LicenseType LicenseType,
    bool HasLicenseKey,
    int Quantity,
    int UsedQuantity,
    int AvailableQuantity,
    DateOnly? PurchaseDate,
    DateOnly? ExpirationDate,
    decimal? Cost,
    DateOnly? SupportEndDate,
    string? Notes,
    bool IsActive);

public record LicenseFilter(int? SoftwareId, int? VendorId, LicenseType? LicenseType, bool? IsActive, DateOnly? ExpiresBefore);

public interface ILicenseData
{
    int SoftwareId { get; }

    int? VendorId { get; }

    int? ContractId { get; }

    string Name { get; }

    LicenseType LicenseType { get; }

    int Quantity { get; }

    DateOnly? PurchaseDate { get; }

    DateOnly? ExpirationDate { get; }

    decimal? Cost { get; }

    DateOnly? SupportEndDate { get; }

    string? Notes { get; }
}

public record CreateLicenseRequest(
    int SoftwareId,
    int? VendorId,
    int? ContractId,
    string Name,
    LicenseType LicenseType,
    string? LicenseKey,
    int Quantity,
    DateOnly? PurchaseDate,
    DateOnly? ExpirationDate,
    decimal? Cost,
    DateOnly? SupportEndDate,
    string? Notes) : ILicenseData;

/// <summary>
/// <paramref name="LicenseKey"/>: null conserva la clave actual; cadena vacia la elimina;
/// cualquier otro valor la reemplaza.
/// </summary>
public record UpdateLicenseRequest(
    int SoftwareId,
    int? VendorId,
    int? ContractId,
    string Name,
    LicenseType LicenseType,
    string? LicenseKey,
    int Quantity,
    DateOnly? PurchaseDate,
    DateOnly? ExpirationDate,
    decimal? Cost,
    DateOnly? SupportEndDate,
    string? Notes,
    bool IsActive) : ILicenseData;

public record LicenseKeyDto(int LicenseId, string? LicenseKey);

public record InstallationDto(
    int Id,
    int LicenseId,
    int AssetId,
    string AssetCode,
    string AssetName,
    int? UserId,
    string? UserName,
    DateTime InstalledAt,
    DateTime? UninstalledAt,
    bool IsActive);

public record InstallLicenseRequest(int AssetId, int? UserId);

/// <summary>Tipos de alerta de licencias (SPECS.md seccion 23).</summary>
public enum LicenseAlertType
{
    /// <summary>Vence dentro de la ventana configurada (Alerts.License.Days).</summary>
    ExpiringSoon = 0,

    Expired = 1,

    /// <summary>Todos los puestos ocupados: la siguiente instalacion seria una sobreasignacion.</summary>
    Exhausted = 2,

    /// <summary>Uso por debajo del umbral configurado (Alerts.License.LowUtilizationPercent).</summary>
    LowUtilization = 3
}

public record LicenseAlertDto(
    int LicenseId,
    string Name,
    string SoftwareName,
    LicenseAlertType AlertType,
    DateOnly? ExpirationDate,
    int? DaysRemaining,
    int? AlertWindowDays,
    int Quantity,
    int UsedQuantity);
