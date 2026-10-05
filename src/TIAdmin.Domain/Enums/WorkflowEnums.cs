namespace TIAdmin.Domain.Enums;

public enum TicketStatus
{
    New = 0,
    Open = 1,
    InProgress = 2,
    WaitingUser = 3,
    WaitingVendor = 4,
    Resolved = 5,
    Closed = 6,
    Cancelled = 7
}

/// <summary>Q-07 / ADR-025: incidentes y solicitudes de servicio comparten la entidad Ticket.</summary>
public enum TicketType
{
    Incident = 0,
    ServiceRequest = 1
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ServiceRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    InProgress = 4,
    Fulfilled = 5,
    RejectedAuto = 6,
    Cancelled = 7
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum MaintenanceType
{
    Preventive = 0,
    Corrective = 1,
    Emergency = 2
}

public enum MaintenanceStatus
{
    Planned = 0,
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ChangeType
{
    Standard = 0,
    Normal = 1,
    Emergency = 2
}

public enum ChangeRisk
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ChangeImpact
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ChangeStatus
{
    Draft = 0,
    Requested = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Implementing = 5,
    Completed = 6,
    RolledBack = 7,
    Closed = 8
}

public enum PurchaseStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Ordered = 4,
    Received = 5,
    Cancelled = 6
}

public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Login = 3,
    Logout = 4,
    Export = 5,
    AccessDenied = 6,

    /// <summary>Lectura de un dato sensible (p. ej. revelar una clave de licencia).</summary>
    SensitiveRead = 7
}

public enum NotificationType
{
    TicketCreated = 0,
    TicketAssigned = 1,
    TicketUpdated = 2,
    TicketOverdue = 3,
    LicenseExpiring = 4,
    ContractExpiring = 5,
    MaintenanceUpcoming = 6,
    ChangeApproved = 7,
    ChangeRejected = 8
}

public enum ConfigurationDataType
{
    String = 0,
    Int = 1,
    Bool = 2,
    Date = 3,
    Json = 4,
    Encrypted = 5
}
