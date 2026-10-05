namespace TIAdmin.Infrastructure.Persistence.Repositories;

using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TIAdminDbContext context;
    private IDepartmentRepository? departments;
    private ILocationRepository? locations;
    private IAssetRepository? assets;
    private IAssetAssignmentRepository? assetAssignments;

    public UnitOfWork(TIAdminDbContext context)
    {
        this.context = context;
    }

    public IDepartmentRepository Departments => departments ??= new DepartmentRepository(context);

    public ILocationRepository Locations => locations ??= new LocationRepository(context);

    public IAssetRepository Assets => assets ??= new AssetRepository(context);

    public IAssetAssignmentRepository AssetAssignments => assetAssignments ??= new AssetAssignmentRepository(context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}