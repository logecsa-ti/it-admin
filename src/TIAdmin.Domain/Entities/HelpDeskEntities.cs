namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Domain.Services;

/// <summary>Categoria de ticket. Define el tipo (incidente/solicitud), prioridad por defecto y si requiere aprobacion.</summary>
public class TicketCategory : AuditableSoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TicketType Type { get; set; } = TicketType.Incident;

    public int? DepartmentId { get; set; }

    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

    /// <summary>Solo aplica a solicitudes de servicio: el ticket nace pendiente de aprobacion.</summary>
    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Politica de SLA (SPECS.md seccion 22). Los criterios nulos aplican a todos; gana la politica
/// mas especifica que coincida (<see cref="SlaPolicySelector"/>).
/// </summary>
public class SlaPolicy : AuditableSoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public TicketPriority? Priority { get; set; }

    public TicketType? TicketType { get; set; }

    public int? DepartmentId { get; set; }

    public int ResponseTimeMinutes { get; set; }

    public int ResolutionTimeMinutes { get; set; }

    public bool BusinessHoursOnly { get; set; } = true;

    public TimeOnly WorkStartTime { get; set; } = new(8, 0);

    public TimeOnly WorkEndTime { get; set; } = new(17, 0);

    /// <summary>Dias laborables, 1 = lunes ... 7 = domingo.</summary>
    public string WorkDays { get; set; } = "1,2,3,4,5";

    /// <summary>Politica de respaldo cuando ninguna otra coincide.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public WorkSchedule Schedule => new(BusinessHoursOnly, WorkStartTime, WorkEndTime, Services.WorkSchedule.ParseDays(WorkDays));

    public bool Matches(int categoryId, TicketPriority priority, TicketType type, int? departmentId) =>
        (CategoryId is null || CategoryId == categoryId)
        && (Priority is null || Priority == priority)
        && (TicketType is null || TicketType == type)
        && (DepartmentId is null || DepartmentId == departmentId);

    /// <summary>La categoria pesa mas que la prioridad, y esta mas que tipo y departamento.</summary>
    public int Specificity =>
        (CategoryId is null ? 0 : 8) + (Priority is null ? 0 : 4) + (TicketType is null ? 0 : 2) + (DepartmentId is null ? 0 : 1);
}

public static class SlaPolicySelector
{
    public static SlaPolicy? Select(IEnumerable<SlaPolicy> policies, int categoryId, TicketPriority priority, TicketType type, int? departmentId)
    {
        var active = policies.Where(p => p.IsActive).ToList();

        return active
                .Where(p => !p.IsDefault && p.Matches(categoryId, priority, type, departmentId))
                .OrderByDescending(p => p.Specificity)
                .ThenBy(p => p.Id)
                .FirstOrDefault()
            ?? active.FirstOrDefault(p => p.IsDefault);
    }
}

/// <summary>
/// Ticket de Help Desk: incidente o solicitud de servicio (Q-07). El estado solo cambia con los
/// metodos del dominio, que aplican el flujo de SPECS.md seccion 21.4 y devuelven el registro de historial.
/// </summary>
public class Ticket : AuditableSoftDeletableEntity
{
    public string TicketNumber { get; set; } = string.Empty;

    public TicketType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; private set; } = TicketStatus.New;

    public int RequesterId { get; set; }

    public int? AssignedToId { get; private set; }

    public int? DepartmentId { get; set; }

    public int? AssetId { get; set; }

    /// <summary>Solicitudes: fecha en que se necesita.</summary>
    public DateOnly? NeededByDate { get; set; }

    public string? ResolutionNotes { get; private set; }

    public DateTime? FirstResponseAt { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public int? SlaPolicyId { get; private set; }

    public DateTime? DueAtResponse { get; private set; }

    public DateTime? DueAtResolution { get; private set; }

    /// <summary>Resultado final del SLA, fijado al resolver. Para tickets abiertos usar <see cref="IsResolutionOverdue"/>.</summary>
    public bool IsSlaBreached { get; private set; }

    /// <summary>Null si la categoria no requiere aprobacion.</summary>
    public ApprovalStatus? ApprovalStatus { get; private set; }

    public int? ApprovedById { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public string? ApprovalComment { get; private set; }

    public TicketCategory? Category { get; set; }

    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.Cancelled);

    public bool IsAwaitingApproval => ApprovalStatus == Enums.ApprovalStatus.Pending;

    public void RequireApproval()
    {
        ApprovalStatus = Enums.ApprovalStatus.Pending;
    }

    /// <summary>Fija la politica y los vencimientos (desde la creacion o desde la aprobacion).</summary>
    public void ApplySla(SlaPolicy? policy, DateTime dueAtResponse, DateTime dueAtResolution)
    {
        SlaPolicyId = policy?.Id;
        DueAtResponse = dueAtResponse;
        DueAtResolution = dueAtResolution;
    }

    public bool IsResponseOverdue(DateTime now) => DueAtResponse is { } due && (FirstResponseAt ?? now) > due;

    public bool IsResolutionOverdue(DateTime now) => DueAtResolution is { } due && (ResolvedAt ?? now) > due;

    /// <summary>Primera accion de un agente (cambio de estado, asignacion o comentario publico).</summary>
    public void RegisterFirstResponse(DateTime now) => FirstResponseAt ??= now;

    public TicketStatusHistory Approve(int approverId, string? approverName, DateTime now, string? comment)
    {
        EnsureAwaitingApproval();
        ApprovalStatus = Enums.ApprovalStatus.Approved;
        ApprovedById = approverId;
        ApprovedAt = now;
        ApprovalComment = comment;
        return History(Status, Status, approverId, approverName, now, comment is null ? "Aprobado" : $"Aprobado: {comment}");
    }

    public TicketStatusHistory Reject(int approverId, string? approverName, DateTime now, string comment)
    {
        EnsureAwaitingApproval();
        ApprovalStatus = Enums.ApprovalStatus.Rejected;
        ApprovedById = approverId;
        ApprovedAt = now;
        ApprovalComment = comment;
        var from = Status;
        Status = TicketStatus.Cancelled;
        return History(from, Status, approverId, approverName, now, $"Rechazado: {comment}");
    }

    /// <summary>
    /// Asigna (o reasigna) el ticket. Un ticket nuevo pasa a Open. Devuelve el historial si el estado cambio.
    /// </summary>
    public TicketStatusHistory? Assign(int assigneeId, int actorId, string? actorName, DateTime now)
    {
        if (!IsOpen)
        {
            throw new ConflictException("TICKET_CLOSED", $"No se puede asignar un ticket en estado {Status}.");
        }

        EnsureNotAwaitingApproval();
        AssignedToId = assigneeId;
        RegisterFirstResponse(now);

        if (Status != TicketStatus.New)
        {
            return null;
        }

        Status = TicketStatus.Open;
        return History(TicketStatus.New, TicketStatus.Open, actorId, actorName, now, "Asignado");
    }

    /// <summary>
    /// Transicion de estado segun <see cref="TicketStatusRules"/>. Resolver exige <paramref name="resolutionNotes"/>.
    /// </summary>
    public TicketStatusHistory ChangeStatus(
        TicketStatus to,
        int actorId,
        string? actorName,
        DateTime now,
        string? comment,
        string? resolutionNotes = null)
    {
        if (to == Status)
        {
            throw new ConflictException("TICKET_STATUS_UNCHANGED", $"El ticket ya esta en estado {Status}.");
        }

        if (!TicketStatusRules.CanChange(Status, to))
        {
            throw new ConflictException("INVALID_TICKET_TRANSITION", $"No se puede pasar de {Status} a {to}.");
        }

        if (to != TicketStatus.Cancelled)
        {
            EnsureNotAwaitingApproval();
        }

        var from = Status;
        switch (to)
        {
            case TicketStatus.Resolved:
                if (string.IsNullOrWhiteSpace(resolutionNotes))
                {
                    throw new DomainValidationException("RESOLUTION_NOTES_REQUIRED", "Para resolver un ticket debe indicar la solucion.");
                }

                ResolutionNotes = resolutionNotes.Trim();
                ResolvedAt = now;
                IsSlaBreached = IsResponseOverdue(now) || IsResolutionOverdue(now);
                break;

            case TicketStatus.Closed:
                ClosedAt = now;
                break;

            case TicketStatus.InProgress when from == TicketStatus.Resolved:
                // Reapertura: la solucion anterior no sirvio; el SLA de resolucion vuelve a evaluarse.
                ResolvedAt = null;
                IsSlaBreached = false;
                break;
        }

        if (from == TicketStatus.New && to != TicketStatus.Cancelled)
        {
            RegisterFirstResponse(now);
        }

        Status = to;
        return History(from, to, actorId, actorName, now, comment);
    }

    private void EnsureAwaitingApproval()
    {
        if (!IsAwaitingApproval)
        {
            throw new ConflictException("TICKET_NOT_PENDING_APPROVAL", "El ticket no esta pendiente de aprobacion.");
        }
    }

    private void EnsureNotAwaitingApproval()
    {
        if (IsAwaitingApproval)
        {
            throw new ConflictException("TICKET_PENDING_APPROVAL", "La solicitud debe aprobarse antes de atenderse.");
        }
    }

    private TicketStatusHistory History(TicketStatus? from, TicketStatus to, int actorId, string? actorName, DateTime now, string? comment) =>
        new()
        {
            TicketId = Id,
            Ticket = this,
            FromStatus = from,
            ToStatus = to,
            UserId = actorId,
            UserName = actorName,
            Comment = comment,
            Timestamp = now
        };
}

/// <summary>Flujo de estados (SPECS.md seccion 21.4) con cancelacion y reapertura.</summary>
public static class TicketStatusRules
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Allowed = new()
    {
        [TicketStatus.New] = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Cancelled],
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.WaitingUser, TicketStatus.WaitingVendor, TicketStatus.Resolved, TicketStatus.Cancelled],
        [TicketStatus.InProgress] = [TicketStatus.WaitingUser, TicketStatus.WaitingVendor, TicketStatus.Resolved, TicketStatus.Cancelled],
        [TicketStatus.WaitingUser] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
        [TicketStatus.WaitingVendor] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [],
        [TicketStatus.Cancelled] = []
    };

    public static bool CanChange(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}

public class TicketComment : AuditableSoftDeletableEntity
{
    public int TicketId { get; set; }

    public int UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>No visible para quien solo accede al ticket como solicitante.</summary>
    public bool IsInternal { get; set; }

    public Ticket? Ticket { get; set; }
}

/// <summary>Historial inmutable de cambios de estado y aprobaciones. UserName es una copia historica.</summary>
public class TicketStatusHistory : Entity
{
    public int TicketId { get; set; }

    public TicketStatus? FromStatus { get; set; }

    public TicketStatus ToStatus { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string? Comment { get; set; }

    public DateTime Timestamp { get; set; }

    public Ticket? Ticket { get; set; }
}
