namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Enums;

public record TicketCategoryDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    TicketType Type,
    int? DepartmentId,
    TicketPriority DefaultPriority,
    bool RequiresApproval,
    bool IsActive);

/// <summary>El codigo se fija al crear y no cambia.</summary>
public record TicketCategoryRequest(
    string Code,
    string Name,
    string? Description,
    TicketType Type,
    int? DepartmentId,
    TicketPriority DefaultPriority,
    bool RequiresApproval,
    bool IsActive);

public record SlaPolicyDto(
    int Id,
    string Name,
    int? CategoryId,
    TicketPriority? Priority,
    TicketType? TicketType,
    int? DepartmentId,
    int ResponseTimeMinutes,
    int ResolutionTimeMinutes,
    bool BusinessHoursOnly,
    TimeOnly WorkStartTime,
    TimeOnly WorkEndTime,
    string WorkDays,
    bool IsDefault,
    bool IsActive);

public record SlaPolicyRequest(
    string Name,
    int? CategoryId,
    TicketPriority? Priority,
    TicketType? TicketType,
    int? DepartmentId,
    int ResponseTimeMinutes,
    int ResolutionTimeMinutes,
    bool BusinessHoursOnly,
    TimeOnly? WorkStartTime,
    TimeOnly? WorkEndTime,
    string? WorkDays,
    bool IsDefault,
    bool IsActive);

/// <summary>
/// Estado del SLA calculado al momento de la consulta: <paramref name="IsSlaBreached"/> es true si
/// la respuesta o la resolucion vencieron (o vencieron antes de producirse).
/// </summary>
public record TicketSlaDto(
    DateTime? DueAtResponse,
    DateTime? DueAtResolution,
    DateTime? FirstResponseAt,
    bool ResponseBreached,
    bool ResolutionBreached,
    bool IsSlaBreached);

public record TicketListItemDto(
    int Id,
    string TicketNumber,
    TicketType Type,
    string Title,
    int CategoryId,
    string CategoryName,
    TicketPriority Priority,
    TicketStatus Status,
    int RequesterId,
    string RequesterName,
    int? AssignedToId,
    string? AssignedToName,
    ApprovalStatus? ApprovalStatus,
    DateTime CreatedAt,
    TicketSlaDto Sla);

public record TicketDetailDto(
    int Id,
    string TicketNumber,
    TicketType Type,
    string Title,
    string Description,
    int CategoryId,
    string CategoryName,
    TicketPriority Priority,
    TicketStatus Status,
    int RequesterId,
    string RequesterName,
    int? AssignedToId,
    string? AssignedToName,
    int? DepartmentId,
    int? AssetId,
    string? AssetCode,
    DateOnly? NeededByDate,
    string? ResolutionNotes,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    ApprovalStatus? ApprovalStatus,
    int? ApprovedById,
    DateTime? ApprovedAt,
    string? ApprovalComment,
    int? SlaPolicyId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    TicketSlaDto Sla);

public record TicketCommentDto(int Id, int UserId, string UserName, string Content, bool IsInternal, DateTime CreatedAt);

public record TicketHistoryDto(int Id, TicketStatus? FromStatus, TicketStatus ToStatus, string? UserName, string? Comment, DateTime Timestamp);

/// <summary>
/// Filtros de <c>GET /api/v1/tickets</c>. <paramref name="Mine"/>: solo los que solicite el usuario;
/// <paramref name="AssignedToMe"/>: los asignados a el; <paramref name="Overdue"/>: abiertos con SLA vencido.
/// </summary>
public record TicketFilter(
    TicketType? Type,
    TicketStatus? Status,
    TicketPriority? Priority,
    int? CategoryId,
    int? AssignedToId,
    int? RequesterId,
    int? DepartmentId,
    bool? Overdue,
    bool? Mine,
    bool? AssignedToMe);

/// <summary>
/// Alta de ticket. El tipo lo define la categoria. <paramref name="RequesterId"/> solo lo usan los
/// agentes (TICKETS.UPDATE) para registrar en nombre de otro; por defecto es el usuario actual.
/// <paramref name="Priority"/> por defecto es la de la categoria.
/// </summary>
public record CreateTicketRequest(
    string Title,
    string Description,
    int CategoryId,
    TicketPriority? Priority,
    int? RequesterId,
    int? DepartmentId,
    int? AssetId,
    DateOnly? NeededByDate);

/// <summary>Edicion por un agente. Cambiar categoria o prioridad recalcula el SLA desde la creacion.</summary>
public record UpdateTicketRequest(
    string Title,
    string Description,
    int CategoryId,
    TicketPriority Priority,
    int? DepartmentId,
    int? AssetId,
    DateOnly? NeededByDate);

public record ChangeTicketStatusRequest(TicketStatus Status, string? Comment, string? ResolutionNotes);

public record AssignTicketRequest(int AssignedToId);

public record ApprovalDecisionRequest(string? Comment);

public record AddTicketCommentRequest(string Content, bool IsInternal);
