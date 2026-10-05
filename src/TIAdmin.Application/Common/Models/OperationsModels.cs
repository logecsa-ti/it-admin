namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

// ---------- Mantenimientos ----------

public record MaintenanceDto(
    int Id,
    string Number,
    string Title,
    string? Description,
    MaintenanceType Type,
    MaintenanceStatus Status,
    int AssetId,
    string AssetCode,
    string AssetName,
    int? TechnicianId,
    string? TechnicianName,
    int? VendorId,
    int? TicketId,
    string? TicketNumber,
    int? ContractId,
    DateTime ScheduledDate,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateOnly? NextDueDate,
    decimal? EstimatedCost,
    decimal? ActualCost,
    string? Findings,
    string? Actions,
    string? Recommendations,
    string? CancellationReason,
    bool IsOverdue);

/// <summary><paramref name="Overdue"/>: planificados/programados cuya fecha ya paso sin iniciarse.</summary>
public record MaintenanceFilter(
    MaintenanceType? Type,
    MaintenanceStatus? Status,
    int? AssetId,
    int? TechnicianId,
    int? VendorId,
    DateTime? ScheduledFrom,
    DateTime? ScheduledTo,
    bool? Overdue);

public interface IMaintenanceData
{
    string Title { get; }

    string? Description { get; }

    MaintenanceType Type { get; }

    int AssetId { get; }

    int? TechnicianId { get; }

    int? VendorId { get; }

    int? TicketId { get; }

    int? ContractId { get; }

    DateTime ScheduledDate { get; }

    decimal? EstimatedCost { get; }
}

public record MaintenanceRequest(
    string Title,
    string? Description,
    MaintenanceType Type,
    int AssetId,
    int? TechnicianId,
    int? VendorId,
    int? TicketId,
    int? ContractId,
    DateTime ScheduledDate,
    decimal? EstimatedCost) : IMaintenanceData;

public record CompleteMaintenanceRequest(string Actions, string? Findings, string? Recommendations, decimal? ActualCost, DateOnly? NextDueDate);

public record CancelRequest(string Reason);

public enum MaintenanceAlertType
{
    /// <summary>Planificado/programado dentro de la ventana (Alerts.Maintenance.Days).</summary>
    Upcoming = 0,

    /// <summary>Planificado/programado cuya fecha ya paso sin iniciarse.</summary>
    Overdue = 1,

    /// <summary>Activo cuyo siguiente preventivo (NextDueDate) vence pronto o ya vencio y no tiene uno planificado.</summary>
    PreventiveDue = 2
}

public record MaintenanceAlertDto(
    MaintenanceAlertType AlertType,
    int AssetId,
    string AssetCode,
    int? MaintenanceId,
    string? MaintenanceNumber,
    DateTime DueDate);

// ---------- Cambios ----------

public record ChangeRequestDto(
    int Id,
    string Number,
    string Title,
    string Description,
    ChangeType Type,
    ChangeRisk Risk,
    ChangeImpact Impact,
    ChangeStatus Status,
    int RequestedById,
    string RequestedByName,
    int? AssignedToId,
    string? AssignedToName,
    int? DepartmentId,
    int? AssetId,
    DateTime? PlannedDate,
    DateTime? ImplementationDate,
    string? RollbackPlan,
    int? ApprovedById,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? ReviewComment,
    string? RejectionReason,
    string? ImplementationNotes,
    DateTime? CompletedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt);

public record ChangeFilter(ChangeType? Type, ChangeStatus? Status, ChangeRisk? Risk, int? RequestedById, int? AssignedToId, bool? AssignedToMe);

public record ChangeRequestBody(
    string Title,
    string Description,
    ChangeType Type,
    ChangeRisk Risk,
    ChangeImpact Impact,
    int? DepartmentId,
    int? AssetId,
    DateTime? PlannedDate,
    string? RollbackPlan);

public record ReviewDecisionRequest(string? Comment);

public record ImplementationNotesRequest(string? Notes);

public record AssignUserRequest(int UserId);

// ---------- Compras ----------

public record PurchaseItemDto(
    int Id,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    int? AssetTypeId,
    int? VendorId,
    string? Notes);

public record PurchaseRequestDto(
    int Id,
    string Number,
    string Title,
    string? Description,
    PurchaseStatus Status,
    int RequestedById,
    string RequestedByName,
    int? DepartmentId,
    int? VendorId,
    string? VendorName,
    string? Justification,
    decimal EstimatedCost,
    DateOnly RequestedDate,
    DateOnly? NeededDate,
    int? ApprovedById,
    DateTime? ApprovedAt,
    string? RejectionReason,
    DateTime? OrderedAt,
    DateTime? ReceivedAt,
    DateTime? CancelledAt,
    IReadOnlyList<PurchaseItemDto> Items);

public record PurchaseFilter(PurchaseStatus? Status, int? RequestedById, int? DepartmentId, int? VendorId);

public record PurchaseItemRequest(string Description, int Quantity, decimal UnitPrice, int? AssetTypeId, int? VendorId, string? Notes);

/// <summary>Alta y edicion (solo en borrador). Las partidas reemplazan a las anteriores.</summary>
public record PurchaseRequestBody(
    string Title,
    string? Description,
    int? DepartmentId,
    int? VendorId,
    string? Justification,
    DateOnly? NeededDate,
    IReadOnlyList<PurchaseItemRequest> Items);
