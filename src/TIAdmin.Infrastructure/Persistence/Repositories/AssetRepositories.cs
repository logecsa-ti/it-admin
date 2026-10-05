namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
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

    public async Task<bool> ExistsAssetCodeAsync(string assetCode, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().AnyAsync(a => a.AssetCode == assetCode, cancellationToken);

    public async Task<bool> ExistsSerialNumberAsync(string serialNumber, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().AnyAsync(
            a => a.SerialNumber == serialNumber && (excludeId == null || a.Id != excludeId),
            cancellationToken);

    public async Task<HashSet<string>> GetExistingAssetCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default) =>
        (await Set.IgnoreQueryFilters().Where(a => codes.Contains(a.AssetCode)).Select(a => a.AssetCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<HashSet<string>> GetExistingSerialNumbersAsync(IReadOnlyCollection<string> serialNumbers, CancellationToken cancellationToken = default) =>
        (await Set.IgnoreQueryFilters().Where(a => a.SerialNumber != null && serialNumbers.Contains(a.SerialNumber)).Select(a => a.SerialNumber!).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<PagedResult<AssetListItemDto>> SearchAsync(
        PagedQuery query,
        AssetFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var assets = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            assets = assets.Where(a => a.AssetCode.Contains(search)
                || a.Name.Contains(search)
                || (a.SerialNumber != null && a.SerialNumber.Contains(search))
                || (a.Brand != null && a.Brand.Contains(search))
                || (a.Model != null && a.Model.Contains(search)));
        }

        if (filter.Status.HasValue)
        {
            assets = assets.Where(a => a.Status == filter.Status.Value);
        }

        if (filter.AssetTypeId.HasValue)
        {
            assets = assets.Where(a => a.AssetTypeId == filter.AssetTypeId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            assets = assets.Where(a => a.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.LocationId.HasValue)
        {
            assets = assets.Where(a => a.LocationId == filter.LocationId.Value);
        }

        if (filter.CurrentUserId.HasValue)
        {
            assets = assets.Where(a => a.CurrentUserId == filter.CurrentUserId.Value);
        }

        if (filter.WarrantyExpiresBefore.HasValue)
        {
            assets = assets.Where(a => a.WarrantyExpiration != null && a.WarrantyExpiration <= filter.WarrantyExpiresBefore.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        assets = query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending ? assets.OrderByDescending(a => a.Name) : assets.OrderBy(a => a.Name),
            "status" => descending ? assets.OrderByDescending(a => a.Status) : assets.OrderBy(a => a.Status),
            "warrantyexpiration" => descending
                ? assets.OrderByDescending(a => a.WarrantyExpiration)
                : assets.OrderBy(a => a.WarrantyExpiration),
            "createdat" => descending ? assets.OrderByDescending(a => a.CreatedAt) : assets.OrderBy(a => a.CreatedAt),
            _ => descending ? assets.OrderByDescending(a => a.AssetCode) : assets.OrderBy(a => a.AssetCode)
        };

        var projected = assets.Select(a => new AssetListItemDto(
            a.Id,
            a.AssetCode,
            a.SerialNumber,
            a.Name,
            a.AssetTypeId,
            a.AssetType!.Name,
            a.Brand,
            a.Model,
            a.Status,
            a.CurrentUserId,
            Context.Users.Where(u => u.Id == a.CurrentUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            a.LocationId,
            a.Location != null ? a.Location.Name : null,
            a.DepartmentId,
            a.Department != null ? a.Department.Name : null,
            a.WarrantyExpiration));

        return await ToPagedResultAsync(projected, query, cancellationToken);
    }

    public async Task<AssetDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        var detail = await Set.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AssetDetailDto(
                a.Id,
                a.AssetCode,
                a.SerialNumber,
                a.Name,
                a.Description,
                a.AssetTypeId,
                a.AssetType!.Name,
                a.Brand,
                a.Model,
                a.PurchaseDate,
                a.PurchaseCost,
                a.WarrantyExpiration,
                a.Status,
                a.LocationId,
                a.Location != null ? a.Location.Name : null,
                a.DepartmentId,
                a.Department != null ? a.Department.Name : null,
                a.VendorId,
                null,
                a.ParentAssetId,
                a.Notes,
                a.CreatedAt,
                a.UpdatedAt,
                null))
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var current = await AssignmentProjection
            .ToDto(Context.AssetAssignments.AsNoTracking().Where(x => x.AssetId == id && x.IsActive), Context)
            .FirstOrDefaultAsync(cancellationToken);

        var vendorNames = await VendorNameLookup.GetAsync(Context, [detail.VendorId], cancellationToken);

        return detail with
        {
            CurrentAssignment = current,
            VendorName = detail.VendorId is { } vendorId ? vendorNames.GetValueOrDefault(vendorId) : null
        };
    }

    public async Task<bool> WouldCreateCycleAsync(int assetId, int candidateParentId, CancellationToken cancellationToken = default)
    {
        var visited = new HashSet<int>();
        int? current = candidateParentId;

        while (current.HasValue)
        {
            if (current.Value == assetId || !visited.Add(current.Value))
            {
                return true;
            }

            var currentId = current.Value;
            current = await Set.AsNoTracking()
                .Where(a => a.Id == currentId)
                .Select(a => a.ParentAssetId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}

public class AssetAssignmentRepository : BaseRepository<AssetAssignment>, IAssetAssignmentRepository
{
    public AssetAssignmentRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<AssetAssignment?> GetActiveAssignmentAsync(int assetId, CancellationToken cancellationToken = default) =>
        await Set.FirstOrDefaultAsync(a => a.AssetId == assetId && a.IsActive, cancellationToken);

    public async Task<PagedResult<AssetAssignmentDto>> SearchAsync(
        PagedQuery query,
        AssignmentFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var assignments = Set.AsNoTracking();

        if (filter.UserId.HasValue)
        {
            assignments = assignments.Where(a => a.UserId == filter.UserId.Value);
        }

        if (filter.AssetId.HasValue)
        {
            assignments = assignments.Where(a => a.AssetId == filter.AssetId.Value);
        }

        if (filter.ActiveOnly == true)
        {
            assignments = assignments.Where(a => a.IsActive);
        }

        // Historial cronologico: lo mas reciente primero salvo que se pida lo contrario.
        assignments = query.SortDirection == SortDirection.Ascending && query.SortBy is not null
            ? assignments.OrderBy(a => a.AssignmentDate).ThenBy(a => a.Id)
            : assignments.OrderByDescending(a => a.AssignmentDate).ThenByDescending(a => a.Id);

        return await ToPagedResultAsync(AssignmentProjection.ToDto(assignments, Context), query, cancellationToken);
    }
}

public sealed class AssetMovementRepository(TIAdminDbContext context) : IAssetMovementRepository
{
    public async Task AddAsync(AssetMovement movement, CancellationToken cancellationToken = default) =>
        await context.AssetMovements.AddAsync(movement, cancellationToken);

    public async Task<PagedResult<AssetMovementDto>> GetByAssetAsync(
        int assetId,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var movements = context.AssetMovements.AsNoTracking()
            .Where(m => m.AssetId == assetId)
            .OrderByDescending(m => m.Timestamp)
            .ThenByDescending(m => m.Id)
            .Select(m => new AssetMovementDto(m.Id, m.MovementType, m.FromValue, m.ToValue, m.UserId, m.UserName, m.Notes, m.Timestamp));

        var total = await movements.LongCountAsync(cancellationToken);
        var items = await movements.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<AssetMovementDto>(items, query.Page, query.PageSize, total);
    }
}

public class AssetTypeRepository : BaseRepository<AssetType>, IAssetTypeRepository
{
    public AssetTypeRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AssetTypeDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(t => isActive == null || t.IsActive == isActive)
            .OrderBy(t => t.Name)
            .Select(t => new AssetTypeDto(t.Id, t.Code, t.Name, t.Description, t.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(t => t.Code == code, cancellationToken);

    public async Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);
}

/// <summary>
/// Proyeccion de asignaciones con nombres de activo y usuarios. Usa IgnoreQueryFilters sobre
/// Assets para que el historial siga mostrando activos dados de baja.
/// </summary>
internal static class AssignmentProjection
{
    public static IQueryable<AssetAssignmentDto> ToDto(IQueryable<AssetAssignment> assignments, TIAdminDbContext context) =>
        assignments.Select(a => new AssetAssignmentDto(
            a.Id,
            a.AssetId,
            context.Assets.IgnoreQueryFilters().Where(x => x.Id == a.AssetId).Select(x => x.AssetCode).First(),
            context.Assets.IgnoreQueryFilters().Where(x => x.Id == a.AssetId).Select(x => x.Name).First(),
            a.UserId,
            context.Users.Where(u => u.Id == a.UserId).Select(u => u.FirstName + " " + u.LastName).First(),
            a.AssignmentDate,
            a.ReturnDate,
            a.AssignedById,
            context.Users.Where(u => u.Id == a.AssignedById).Select(u => u.FirstName + " " + u.LastName).First(),
            a.ReturnedById,
            context.Users.Where(u => u.Id == a.ReturnedById).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            a.ConditionAtAssignment,
            a.ConditionAtReturn,
            a.Notes,
            a.IsActive));
}
