namespace TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Agrupa los repositorios sobre un mismo DbContext. El DbContext pertenece al contenedor
/// de DI (scoped), por lo que el UnitOfWork no lo libera.
/// </summary>
public interface IUnitOfWork
{
    IDepartmentRepository Departments { get; }

    ILocationRepository Locations { get; }

    IAssetRepository Assets { get; }

    IAssetAssignmentRepository AssetAssignments { get; }

    IAssetMovementRepository AssetMovements { get; }

    IAssetTypeRepository AssetTypes { get; }

    IVendorRepository Vendors { get; }

    IContractRepository Contracts { get; }

    ISoftwareRepository Software { get; }

    ISoftwareLicenseRepository Licenses { get; }

    ISoftwareInstallationRepository Installations { get; }

    ITicketCategoryRepository TicketCategories { get; }

    ISlaPolicyRepository SlaPolicies { get; }

    ITicketRepository Tickets { get; }

    IMaintenanceRepository Maintenances { get; }

    IChangeRequestRepository ChangeRequests { get; }

    IPurchaseRequestRepository PurchaseRequests { get; }

    IAuditLogRepository AuditLogs { get; }

    ISystemConfigurationRepository Configurations { get; }

    IDocumentRepository Documents { get; }

    INotificationRepository Notifications { get; }

    IExportJobRepository ExportJobs { get; }

    /// <summary>
    /// Persiste los cambios. Una violacion de indice unico (p. ej. dos asignaciones
    /// simultaneas del mismo activo) se traduce a <see cref="Domain.Exceptions.ConflictException"/>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
