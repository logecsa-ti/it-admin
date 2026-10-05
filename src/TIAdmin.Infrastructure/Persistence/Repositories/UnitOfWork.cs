namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    /// <summary>Violacion de indice unico (2601) o de restriccion UNIQUE/PK (2627).</summary>
    private static readonly int[] UniqueViolationNumbers = [2601, 2627];

    private readonly TIAdminDbContext context;
    private IDepartmentRepository? departments;
    private ILocationRepository? locations;
    private IAssetRepository? assets;
    private IAssetAssignmentRepository? assetAssignments;
    private IAssetMovementRepository? assetMovements;
    private IAssetTypeRepository? assetTypes;
    private IVendorRepository? vendors;
    private IContractRepository? contracts;
    private ISoftwareRepository? software;
    private ISoftwareLicenseRepository? licenses;
    private ISoftwareInstallationRepository? installations;
    private ITicketCategoryRepository? ticketCategories;
    private ISlaPolicyRepository? slaPolicies;
    private ITicketRepository? tickets;
    private IMaintenanceRepository? maintenances;
    private IChangeRequestRepository? changeRequests;
    private IPurchaseRequestRepository? purchaseRequests;
    private IAuditLogRepository? auditLogs;
    private ISystemConfigurationRepository? configurations;

    public UnitOfWork(TIAdminDbContext context)
    {
        this.context = context;
    }

    public IDepartmentRepository Departments => departments ??= new DepartmentRepository(context);

    public ILocationRepository Locations => locations ??= new LocationRepository(context);

    public IAssetRepository Assets => assets ??= new AssetRepository(context);

    public IAssetAssignmentRepository AssetAssignments => assetAssignments ??= new AssetAssignmentRepository(context);

    public IAssetMovementRepository AssetMovements => assetMovements ??= new AssetMovementRepository(context);

    public IAssetTypeRepository AssetTypes => assetTypes ??= new AssetTypeRepository(context);

    public IVendorRepository Vendors => vendors ??= new VendorRepository(context);

    public IContractRepository Contracts => contracts ??= new ContractRepository(context);

    public ISoftwareRepository Software => software ??= new SoftwareRepository(context);

    public ISoftwareLicenseRepository Licenses => licenses ??= new SoftwareLicenseRepository(context);

    public ISoftwareInstallationRepository Installations => installations ??= new SoftwareInstallationRepository(context);

    public ITicketCategoryRepository TicketCategories => ticketCategories ??= new TicketCategoryRepository(context);

    public ISlaPolicyRepository SlaPolicies => slaPolicies ??= new SlaPolicyRepository(context);

    public ITicketRepository Tickets => tickets ??= new TicketRepository(context);

    public IMaintenanceRepository Maintenances => maintenances ??= new MaintenanceRepository(context);

    public IChangeRequestRepository ChangeRequests => changeRequests ??= new ChangeRequestRepository(context);

    public IPurchaseRequestRepository PurchaseRequests => purchaseRequests ??= new PurchaseRequestRepository(context);

    public IAuditLogRepository AuditLogs => auditLogs ??= new AuditLogRepository(context);

    public ISystemConfigurationRepository Configurations => configurations ??= new SystemConfigurationRepository(context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql
            && UniqueViolationNumbers.Contains(sql.Number))
        {
            // Las validaciones previas cubren el caso normal; esto atrapa carreras entre peticiones
            // (p. ej. dos asignaciones simultaneas del mismo activo).
            throw new ConflictException("DUPLICATE_RECORD",
                "La operacion entra en conflicto con un registro existente. Recargue e intente nuevamente.");
        }
    }
}
