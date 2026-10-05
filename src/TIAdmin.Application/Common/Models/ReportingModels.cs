namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

// ---------- Auditoria ----------

public record AuditLogDto(
    long Id,
    DateTime Timestamp,
    int? UserId,
    string? UserName,
    AuditAction Action,
    string Module,
    string EntityName,
    string EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId);

public record AuditFilter(
    int? UserId,
    AuditAction? Action,
    string? Module,
    string? EntityName,
    string? EntityId,
    string? IpAddress,
    string? CorrelationId,
    DateTime? From,
    DateTime? To);

// ---------- Configuracion ----------

/// <summary>
/// <paramref name="Value"/> se oculta ("********") si el parametro es cifrado. <paramref name="IsRuntimeManaged"/>:
/// lo define la configuracion de despliegue (appsettings) y no se edita por API.
/// </summary>
public record ConfigurationItemDto(
    string Key,
    string? Value,
    string? DefaultValue,
    string Group,
    ConfigurationDataType DataType,
    string? Description,
    bool IsPublic,
    bool IsEditable,
    bool IsRuntimeManaged,
    DateTime? UpdatedAt);

public record UpdateConfigurationRequest(string? Value);

// ---------- Dashboard ----------

public record CountByLabel(string Label, int Count);

public record AssetKpis(int Total, int Assigned, IReadOnlyList<CountByLabel> ByStatus, IReadOnlyList<CountByLabel> ByDepartment, IReadOnlyList<CountByLabel> ByLocation);

/// <summary><paramref name="SlaCompliancePercent"/>: tickets resueltos en los ultimos 30 dias dentro de su SLA; null si no hubo.</summary>
public record TicketKpis(int Open, int Overdue, int CreatedLast7Days, IReadOnlyList<CountByLabel> OpenByPriority, decimal? SlaCompliancePercent);

/// <summary><paramref name="ValidPercent"/>: licencias activas no vencidas sobre el total de activas.</summary>
public record LicenseKpis(int Active, int ExpiringSoon, int Expired, decimal? ValidPercent);

public record ContractKpis(int Active, int ExpiringSoon, int Expired);

public record MaintenanceKpis(int Pending, int Overdue, int InProgress);

/// <summary>Costos del anio en curso (zona de la organizacion).</summary>
public record CostKpis(int Year, decimal Purchases, decimal Maintenance, decimal Licenses, decimal Total);

public record MyKpis(int OpenTickets, int AssignedAssets, int TicketsAssignedToMe);

/// <summary>Cada seccion es null si el usuario no tiene el permiso de su modulo (ADR-032).</summary>
public record DashboardSummaryDto(
    DateTime GeneratedAt,
    MyKpis Mine,
    AssetKpis? Assets,
    TicketKpis? Tickets,
    LicenseKpis? Licenses,
    ContractKpis? Contracts,
    MaintenanceKpis? Maintenance,
    CostKpis? Costs);

// ---------- Reportes ----------

public record AssetSummaryReport(
    int Total,
    decimal TotalPurchaseCost,
    IReadOnlyList<CountByLabel> ByStatus,
    IReadOnlyList<CountByLabel> ByType,
    IReadOnlyList<CountByLabel> ByDepartment,
    IReadOnlyList<CountByLabel> ByLocation,
    int WarrantyExpiringIn90Days);

public record AssetsByUserRow(int UserId, string UserName, string? Email, int AssetCount, IReadOnlyList<string> AssetCodes);

/// <summary>Tickets creados en el periodo [From, To).</summary>
public record TicketReport(
    DateTime From,
    DateTime To,
    int Created,
    int Resolved,
    int Open,
    IReadOnlyList<CountByLabel> ByStatus,
    IReadOnlyList<CountByLabel> ByPriority,
    IReadOnlyList<CountByLabel> ByCategory,
    IReadOnlyList<CountByLabel> ByTechnician,
    IReadOnlyList<CountByLabel> ByType,
    decimal? AverageResolutionHours);

public record SlaComplianceRow(string Label, int Resolved, int WithinSla, decimal? CompliancePercent);

/// <summary>Cumplimiento de SLA de los tickets resueltos en el periodo.</summary>
public record SlaReport(
    DateTime From,
    DateTime To,
    int Resolved,
    int WithinSla,
    decimal? CompliancePercent,
    IReadOnlyList<SlaComplianceRow> ByPriority,
    IReadOnlyList<SlaComplianceRow> ByCategory);

public record LicenseUsageReportRow(
    int SoftwareId,
    string Software,
    int Licenses,
    int Seats,
    int UsedSeats,
    decimal? UtilizationPercent,
    int Expired,
    DateOnly? NextExpiration);

public record CostByMonth(int Year, int Month, decimal Purchases, decimal Maintenance, decimal Licenses, decimal Total);

/// <summary>Costos de TI del periodo: compras ordenadas/recibidas, mantenimientos completados y licencias adquiridas.</summary>
public record CostReport(DateTime From, DateTime To, decimal Purchases, decimal Maintenance, decimal Licenses, decimal Total, IReadOnlyList<CostByMonth> ByMonth);
