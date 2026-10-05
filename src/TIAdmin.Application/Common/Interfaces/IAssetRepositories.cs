namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public interface IAssetRepository : IRepository<Asset>
{
    Task<Asset?> GetByAssetCodeAsync(string assetCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Asset>> GetByStatusAsync(AssetStatus status, CancellationToken cancellationToken = default);

    /// <summary>Incluye eliminados: el indice unico de AssetCode los cubre.</summary>
    Task<bool> ExistsAssetCodeAsync(string assetCode, CancellationToken cancellationToken = default);

    /// <summary>Incluye eliminados: el indice unico de SerialNumber los cubre.</summary>
    Task<bool> ExistsSerialNumberAsync(string serialNumber, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Codigos ya usados (incluye eliminados) entre los indicados: validacion masiva de importaciones.</summary>
    Task<HashSet<string>> GetExistingAssetCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);

    /// <summary>Series ya registradas (incluye eliminados) entre las indicadas.</summary>
    Task<HashSet<string>> GetExistingSerialNumbersAsync(IReadOnlyCollection<string> serialNumbers, CancellationToken cancellationToken = default);

    Task<PagedResult<AssetListItemDto>> SearchAsync(PagedQuery query, AssetFilter filter, CancellationToken cancellationToken = default);

    Task<AssetDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Indica si usar <paramref name="candidateParentId"/> como padre crearia un ciclo.</summary>
    Task<bool> WouldCreateCycleAsync(int assetId, int candidateParentId, CancellationToken cancellationToken = default);
}

public interface IAssetAssignmentRepository : IRepository<AssetAssignment>
{
    /// <summary>Asignacion vigente, con seguimiento de cambios (para cerrarla).</summary>
    Task<AssetAssignment?> GetActiveAssignmentAsync(int assetId, CancellationToken cancellationToken = default);

    Task<PagedResult<AssetAssignmentDto>> SearchAsync(PagedQuery query, AssignmentFilter filter, CancellationToken cancellationToken = default);
}

public interface IAssetMovementRepository
{
    Task AddAsync(AssetMovement movement, CancellationToken cancellationToken = default);

    Task<PagedResult<AssetMovementDto>> GetByAssetAsync(int assetId, PagedQuery query, CancellationToken cancellationToken = default);
}

public interface IAssetTypeRepository : IRepository<AssetType>
{
    Task<IReadOnlyList<AssetTypeDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);

    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Consulta minima de usuarios para Application, que no conoce las entidades de Identity.
/// </summary>
public interface IUserDirectory
{
    Task<UserReference?> FindAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Solo usuarios activos.</summary>
    Task<IReadOnlyList<UserReference>> FindActiveAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>Ids de usuarios activos con el permiso (por rol o asignado directamente).</summary>
    Task<IReadOnlyList<int>> FindActiveWithPermissionAsync(string permission, CancellationToken cancellationToken = default);
}

public record UserReference(int Id, string UserName, string FullName, bool IsActive, string? Email = null)
{
    /// <summary>Etiqueta legible para historiales: "jperez (Juan Perez)".</summary>
    public string Label => string.IsNullOrWhiteSpace(FullName) ? UserName : $"{UserName} ({FullName})";
}
