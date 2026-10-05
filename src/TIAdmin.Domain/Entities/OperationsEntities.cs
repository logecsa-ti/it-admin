namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Mantenimiento de un activo (SPECS.md seccion 26). El estado solo cambia con
/// Schedule / Start / Complete / Cancel.
/// </summary>
public class Maintenance : AuditableSoftDeletableEntity
{
    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public MaintenanceType Type { get; set; }

    public MaintenanceStatus Status { get; private set; } = MaintenanceStatus.Planned;

    public int AssetId { get; set; }

    public int? TechnicianId { get; set; }

    public int? VendorId { get; set; }

    public int? TicketId { get; set; }

    public int? ContractId { get; set; }

    public DateTime ScheduledDate { get; set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    /// <summary>Preventivo: fecha sugerida del siguiente mantenimiento.</summary>
    public DateOnly? NextDueDate { get; private set; }

    public decimal? EstimatedCost { get; set; }

    public decimal? ActualCost { get; private set; }

    public string? Findings { get; private set; }

    public string? Actions { get; private set; }

    public string? Recommendations { get; private set; }

    public string? CancellationReason { get; private set; }

    public bool IsClosed => Status is MaintenanceStatus.Completed or MaintenanceStatus.Cancelled;

    public void Schedule(DateTime scheduledDate)
    {
        if (Status is not (MaintenanceStatus.Planned or MaintenanceStatus.Scheduled))
        {
            throw new ConflictException("INVALID_MAINTENANCE_TRANSITION", $"No se puede programar un mantenimiento en estado {Status}.");
        }

        ScheduledDate = scheduledDate;
        Status = MaintenanceStatus.Scheduled;
    }

    public void Start(DateTime now)
    {
        if (Status is not (MaintenanceStatus.Planned or MaintenanceStatus.Scheduled))
        {
            throw new ConflictException("INVALID_MAINTENANCE_TRANSITION", $"No se puede iniciar un mantenimiento en estado {Status}.");
        }

        StartedAt = now;
        Status = MaintenanceStatus.InProgress;
    }

    public void Complete(DateTime now, string actions, string? findings, string? recommendations, decimal? actualCost, DateOnly? nextDueDate)
    {
        if (Status != MaintenanceStatus.InProgress)
        {
            throw new ConflictException("INVALID_MAINTENANCE_TRANSITION", $"Solo un mantenimiento en curso puede completarse (estado: {Status}).");
        }

        if (string.IsNullOrWhiteSpace(actions))
        {
            throw new DomainValidationException("MAINTENANCE_ACTIONS_REQUIRED", "Indique las acciones realizadas.");
        }

        if (actualCost < 0)
        {
            throw new DomainValidationException("INVALID_COST", "El costo real no puede ser negativo.");
        }

        Actions = actions.Trim();
        Findings = findings;
        Recommendations = recommendations;
        ActualCost = actualCost;
        NextDueDate = nextDueDate;
        CompletedAt = now;
        Status = MaintenanceStatus.Completed;
    }

    public void Cancel(string reason)
    {
        if (IsClosed)
        {
            throw new ConflictException("INVALID_MAINTENANCE_TRANSITION", $"No se puede cancelar un mantenimiento en estado {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("CANCELLATION_REASON_REQUIRED", "Indique el motivo de la cancelacion.");
        }

        CancellationReason = reason.Trim();
        Status = MaintenanceStatus.Cancelled;
    }
}

/// <summary>
/// Solicitud de cambio (SPECS.md seccion 27). Flujo: Draft → Requested → (UnderReview) → Approved/Rejected →
/// Implementing → Completed/RolledBack → Closed. Los cambios estandar quedan preaprobados al enviarse.
/// </summary>
public class ChangeRequest : AuditableSoftDeletableEntity
{
    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ChangeType Type { get; set; }

    public ChangeRisk Risk { get; set; }

    public ChangeImpact Impact { get; set; }

    public ChangeStatus Status { get; private set; } = ChangeStatus.Draft;

    public int RequestedById { get; set; }

    public int? AssignedToId { get; private set; }

    public int? DepartmentId { get; set; }

    public int? AssetId { get; set; }

    public DateTime? PlannedDate { get; set; }

    public DateTime? ImplementationDate { get; private set; }

    public string? RollbackPlan { get; set; }

    public int? ApprovedById { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>Notas de cierre de la implementacion (resultado o motivo del rollback).</summary>
    public string? ImplementationNotes { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public void EnsureEditable()
    {
        if (Status != ChangeStatus.Draft)
        {
            throw new ConflictException("CHANGE_NOT_EDITABLE", $"Solo un cambio en borrador puede editarse (estado: {Status}).");
        }
    }

    public void Submit(DateTime now)
    {
        Require(ChangeStatus.Draft);

        if (Type != ChangeType.Standard && string.IsNullOrWhiteSpace(RollbackPlan))
        {
            throw new DomainValidationException("ROLLBACK_PLAN_REQUIRED", "Un cambio normal o de emergencia requiere plan de rollback.");
        }

        if (Type == ChangeType.Standard)
        {
            // Cambio estandar: procedimiento conocido y preaprobado (ITIL).
            Status = ChangeStatus.Approved;
            ApprovedAt = now;
            ReviewComment = "Cambio estandar preaprobado";
            return;
        }

        Status = ChangeStatus.Requested;
    }

    public void StartReview() => Transition([ChangeStatus.Requested], ChangeStatus.UnderReview);

    public void Approve(int approverId, DateTime now, string? comment)
    {
        EnsureNotSelfApproval(approverId);
        Transition([ChangeStatus.Requested, ChangeStatus.UnderReview], ChangeStatus.Approved);
        ApprovedById = approverId;
        ApprovedAt = now;
        ReviewComment = comment;
    }

    public void Reject(int approverId, DateTime now, string reason)
    {
        EnsureNotSelfApproval(approverId);
        Transition([ChangeStatus.Requested, ChangeStatus.UnderReview], ChangeStatus.Rejected);
        ApprovedById = approverId;
        ApprovedAt = now;
        RejectionReason = reason;
    }

    public void Assign(int userId)
    {
        if (Status is ChangeStatus.Closed or ChangeStatus.Rejected or ChangeStatus.Completed or ChangeStatus.RolledBack)
        {
            throw new ConflictException("INVALID_CHANGE_TRANSITION", $"No se puede asignar un cambio en estado {Status}.");
        }

        AssignedToId = userId;
    }

    public void StartImplementation(DateTime now)
    {
        Transition([ChangeStatus.Approved], ChangeStatus.Implementing);
        ImplementationDate = now;
    }

    public void Complete(DateTime now, string? notes)
    {
        Transition([ChangeStatus.Implementing], ChangeStatus.Completed);
        CompletedAt = now;
        ImplementationNotes = notes;
    }

    public void RollBack(DateTime now, string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainValidationException("ROLLBACK_NOTES_REQUIRED", "Indique el motivo del rollback.");
        }

        Transition([ChangeStatus.Implementing], ChangeStatus.RolledBack);
        CompletedAt = now;
        ImplementationNotes = notes.Trim();
    }

    public void Close(DateTime now)
    {
        Transition([ChangeStatus.Completed, ChangeStatus.RolledBack, ChangeStatus.Rejected], ChangeStatus.Closed);
        ClosedAt = now;
    }

    /// <summary>Segregacion de funciones: quien solicita no aprueba.</summary>
    private void EnsureNotSelfApproval(int approverId)
    {
        if (approverId == RequestedById)
        {
            throw new ConflictException("SELF_APPROVAL_NOT_ALLOWED", "Quien solicita un cambio no puede aprobarlo ni rechazarlo.");
        }
    }

    private void Require(ChangeStatus expected)
    {
        if (Status != expected)
        {
            throw new ConflictException("INVALID_CHANGE_TRANSITION", $"Operacion no valida en estado {Status}.");
        }
    }

    private void Transition(ChangeStatus[] from, ChangeStatus to)
    {
        if (!from.Contains(Status))
        {
            throw new ConflictException("INVALID_CHANGE_TRANSITION", $"No se puede pasar de {Status} a {to}.");
        }

        Status = to;
    }
}

/// <summary>
/// Solicitud de compra de TI con sus partidas. El costo estimado es la suma de las partidas y
/// las partidas solo se editan en borrador.
/// </summary>
public class PurchaseRequest : AuditableSoftDeletableEntity
{
    private readonly List<PurchaseItem> items = [];

    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PurchaseStatus Status { get; private set; } = PurchaseStatus.Draft;

    public int RequestedById { get; set; }

    public int? DepartmentId { get; set; }

    public int? VendorId { get; set; }

    public string? Justification { get; set; }

    public decimal EstimatedCost { get; private set; }

    public DateOnly RequestedDate { get; set; }

    public DateOnly? NeededDate { get; set; }

    public int? ApprovedById { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime? OrderedAt { get; private set; }

    public DateTime? ReceivedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<PurchaseItem> Items => items;

    public void EnsureEditable()
    {
        if (Status != PurchaseStatus.Draft)
        {
            throw new ConflictException("PURCHASE_NOT_EDITABLE", $"Solo una solicitud en borrador puede editarse (estado: {Status}).");
        }
    }

    public void ReplaceItems(IEnumerable<PurchaseItem> newItems)
    {
        ArgumentNullException.ThrowIfNull(newItems);
        EnsureEditable();

        var list = newItems.ToList();
        foreach (var item in list)
        {
            if (item.Quantity <= 0)
            {
                throw new DomainValidationException("INVALID_ITEM_QUANTITY", "La cantidad de cada partida debe ser mayor que cero.");
            }

            if (item.UnitPrice < 0)
            {
                throw new DomainValidationException("INVALID_ITEM_PRICE", "El precio unitario no puede ser negativo.");
            }

            item.TotalPrice = item.Quantity * item.UnitPrice;
        }

        items.Clear();
        items.AddRange(list);
        EstimatedCost = items.Sum(i => i.TotalPrice);
    }

    public void Submit()
    {
        if (Status == PurchaseStatus.Draft && items.Count == 0)
        {
            throw new DomainValidationException("PURCHASE_ITEMS_REQUIRED", "La solicitud debe tener al menos una partida.");
        }

        Transition([PurchaseStatus.Draft], PurchaseStatus.Submitted);
    }

    public void Approve(int approverId, DateTime now)
    {
        EnsureNotSelfApproval(approverId);
        Transition([PurchaseStatus.Submitted], PurchaseStatus.Approved);
        ApprovedById = approverId;
        ApprovedAt = now;
    }

    public void Reject(int approverId, DateTime now, string reason)
    {
        EnsureNotSelfApproval(approverId);
        Transition([PurchaseStatus.Submitted], PurchaseStatus.Rejected);
        ApprovedById = approverId;
        ApprovedAt = now;
        RejectionReason = reason;
    }

    public void MarkOrdered(DateTime now)
    {
        if (Status == PurchaseStatus.Approved && VendorId is null && items.Any(i => i.VendorId is null))
        {
            throw new DomainValidationException("PURCHASE_VENDOR_REQUIRED",
                "Para ordenar, indique el proveedor de la solicitud o de cada partida.");
        }

        Transition([PurchaseStatus.Approved], PurchaseStatus.Ordered);
        OrderedAt = now;
    }

    public void MarkReceived(DateTime now)
    {
        Transition([PurchaseStatus.Ordered], PurchaseStatus.Received);
        ReceivedAt = now;
    }

    public void Cancel(DateTime now)
    {
        Transition([PurchaseStatus.Draft, PurchaseStatus.Submitted, PurchaseStatus.Approved], PurchaseStatus.Cancelled);
        CancelledAt = now;
    }

    private void EnsureNotSelfApproval(int approverId)
    {
        if (approverId == RequestedById)
        {
            throw new ConflictException("SELF_APPROVAL_NOT_ALLOWED", "Quien solicita una compra no puede aprobarla ni rechazarla.");
        }
    }

    private void Transition(PurchaseStatus[] from, PurchaseStatus to)
    {
        if (!from.Contains(Status))
        {
            throw new ConflictException("INVALID_PURCHASE_TRANSITION", $"No se puede pasar de {Status} a {to}.");
        }

        Status = to;
    }
}

public class PurchaseItem : Entity
{
    public int PurchaseRequestId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    /// <summary>Cantidad x precio unitario, calculado por <see cref="PurchaseRequest.ReplaceItems"/>.</summary>
    public decimal TotalPrice { get; set; }

    public int? AssetTypeId { get; set; }

    public int? VendorId { get; set; }

    public string? Notes { get; set; }
}
