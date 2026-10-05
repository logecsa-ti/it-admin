namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public class AssetRepository : BaseRepository<Asset>, IAssetRepository
{
    public AssetRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<Asset?> GetByAssetCodeAsync(string assetCode, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(a => a.AssetCode == assetCode, cancellationToken);

    public async Task<IReadOnlyList<Asset>> GetByStatusAsync(AssetStatus status, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(a => a.Status == status).ToListAsync(cancellationToken);
}

public class AssetAssignmentRepository : BaseRepository<AssetAssignment>, IAssetAssignmentRepository
{
    public AssetAssignmentRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<AssetAssignment?> GetActiveAssignmentAsync(int assetId, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(a => a.AssetId == assetId && a.IsActive, cancellationToken);
}
