namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Domain.Entities;

public interface IAssetRepository : IRepository<Asset>
{
    Task<Asset?> GetByAssetCodeAsync(string assetCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> GetByStatusAsync(TIAdmin.Domain.Enums.AssetStatus status, CancellationToken cancellationToken = default);
}

public interface IAssetAssignmentRepository : IRepository<AssetAssignment>
{
    Task<AssetAssignment?> GetActiveAssignmentAsync(int assetId, CancellationToken cancellationToken = default);
}