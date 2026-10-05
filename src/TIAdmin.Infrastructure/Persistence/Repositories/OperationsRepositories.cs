namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public class MaintenanceRepository : BaseRepository<Maintenance>, IMaintenanceRepository
{
    public MaintenanceRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<MaintenanceDto>> SearchAsync(
        PagedQuery query,
        MaintenanceFilter filter,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var maintenances = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            maintenances = maintenances.Where(m => m.Number.Contains(search) || m.Title.Contains(search));
        }

        if (filter.Type.HasValue)
        {
            maintenances = maintenances.Where(m => m.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            maintenances = maintenances.Where(m => m.Status == filter.Status.Value);
        }

        if (filter.AssetId.HasValue)
        {
            maintenances = maintenances.Where(m => m.AssetId == filter.AssetId.Value);
        }

        if (filter.TechnicianId.HasValue)
        {
            maintenances = maintenances.Where(m => m.TechnicianId == filter.TechnicianId.Value);
        }

        if (filter.VendorId.HasValue)
        {
            maintenances = maintenances.Where(m => m.VendorId == filter.VendorId.Value);
        }

        if (filter.ScheduledFrom.HasValue)
        {
            maintenances = maintenances.Where(m => m.ScheduledDate >= filter.ScheduledFrom.Value);
        }

        if (filter.ScheduledTo.HasValue)
        {
            maintenances = maintenances.Where(m => m.ScheduledDate <= filter.ScheduledTo.Value);
        }

        if (filter.Overdue == true)
        {
            maintenances = maintenances.Where(m =>
                (m.Status == MaintenanceStatus.Planned || m.Status == MaintenanceStatus.Scheduled) && m.ScheduledDate < now);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        maintenances = query.SortBy?.ToLowerInvariant() switch
        {
            "number" => descending ? maintenances.OrderByDescending(m => m.Id) : maintenances.OrderBy(m => m.Id),
            "status" => descending ? maintenances.OrderByDescending(m => m.Status) : maintenances.OrderBy(m => m.Status),
            "completedat" => descending ? maintenances.OrderByDescending(m => m.CompletedAt) : maintenances.OrderBy(m => m.CompletedAt),
            _ => descending ? maintenances.OrderByDescending(m => m.ScheduledDate) : maintenances.OrderBy(m => m.ScheduledDate)
        };

        var total = await maintenances.LongCountAsync(cancellationToken);
        var rows = await Project(maintenances.Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        var items = await CompleteAsync(rows, now, cancellationToken);
        return new PagedResult<MaintenanceDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<MaintenanceDto?> GetDtoAsync(int id, DateTime now, CancellationToken cancellationToken = default)
    {
        var rows = await Project(Set.AsNoTracking().Where(m => m.Id == id)).ToListAsync(cancellationToken);
        return (await CompleteAsync(rows, now, cancellationToken)).FirstOrDefault();
    }

    public async Task<bool> HasOtherInProgressAsync(int assetId, int excludeId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(m => m.AssetId == assetId && m.Id != excludeId && m.Status == MaintenanceStatus.InProgress, cancellationToken);

    public async Task<IReadOnlyList<MaintenanceAlertDto>> GetPendingUntilAsync(DateTime until, DateTime now, CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(m => (m.Status == MaintenanceStatus.Planned || m.Status == MaintenanceStatus.Scheduled) && m.ScheduledDate <= until)
            .Select(m => new { m.Id, m.Number, m.AssetId, m.ScheduledDate })
            .ToListAsync(cancellationToken);

        var assets = await AssetLabelLookup.GetAsync(Context, rows.Select(r => r.AssetId), cancellationToken);
        return rows
            .Select(r => new MaintenanceAlertDto(
                r.ScheduledDate < now ? MaintenanceAlertType.Overdue : MaintenanceAlertType.Upcoming,
                r.AssetId, assets.GetValueOrDefault(r.AssetId).Code ?? string.Empty, r.Id, r.Number, r.ScheduledDate))
            .ToList();
    }

    public async Task<IReadOnlyList<MaintenanceAlertDto>> GetPreventiveDueAsync(DateOnly until, CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(m => m.Status == MaintenanceStatus.Completed && m.NextDueDate != null && m.NextDueDate <= until
                && !Set.Any(o => o.AssetId == m.AssetId && o.Id != m.Id
                    && (o.Status == MaintenanceStatus.Planned || o.Status == MaintenanceStatus.Scheduled
                        || o.Status == MaintenanceStatus.InProgress
                        || (o.Status == MaintenanceStatus.Completed && o.CompletedAt > m.CompletedAt))))
            .Select(m => new { m.Id, m.Number, m.AssetId, NextDueDate = m.NextDueDate!.Value })
            .ToListAsync(cancellationToken);

        var assets = await AssetLabelLookup.GetAsync(Context, rows.Select(r => r.AssetId), cancellationToken);
        return rows
            .Select(r => new MaintenanceAlertDto(MaintenanceAlertType.PreventiveDue, r.AssetId,
                assets.GetValueOrDefault(r.AssetId).Code ?? string.Empty, r.Id, r.Number, r.NextDueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)))
            .ToList();
    }

    private IQueryable<MaintenanceDto> Project(IQueryable<Maintenance> maintenances) =>
        maintenances.Select(m => new MaintenanceDto(
            m.Id, m.Number, m.Title, m.Description, m.Type, m.Status, m.AssetId, string.Empty, string.Empty,
            m.TechnicianId,
            Context.Users.Where(u => u.Id == m.TechnicianId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            m.VendorId,
            m.TicketId,
            Context.Tickets.Where(t => t.Id == m.TicketId).Select(t => t.TicketNumber).FirstOrDefault(),
            m.ContractId, m.ScheduledDate, m.StartedAt, m.CompletedAt, m.NextDueDate, m.EstimatedCost, m.ActualCost,
            m.Findings, m.Actions, m.Recommendations, m.CancellationReason, false));

    /// <summary>Completa el activo (incluidos dados de baja) y el indicador de vencido.</summary>
    private async Task<IReadOnlyList<MaintenanceDto>> CompleteAsync(List<MaintenanceDto> rows, DateTime now, CancellationToken cancellationToken)
    {
        var assets = await AssetLabelLookup.GetAsync(Context, rows.Select(r => r.AssetId), cancellationToken);
        return rows
            .Select(r =>
            {
                var asset = assets.GetValueOrDefault(r.AssetId);
                return r with
                {
                    AssetCode = asset.Code ?? string.Empty,
                    AssetName = asset.Name ?? string.Empty,
                    IsOverdue = r.Status is MaintenanceStatus.Planned or MaintenanceStatus.Scheduled && r.ScheduledDate < now
                };
            })
            .ToList();
    }
}

public class ChangeRequestRepository : BaseRepository<ChangeRequest>, IChangeRequestRepository
{
    public ChangeRequestRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<ChangeRequestDto>> SearchAsync(
        PagedQuery query,
        ChangeFilter filter,
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var changes = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            changes = changes.Where(c => c.Number.Contains(search) || c.Title.Contains(search));
        }

        if (filter.Type.HasValue)
        {
            changes = changes.Where(c => c.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            changes = changes.Where(c => c.Status == filter.Status.Value);
        }

        if (filter.Risk.HasValue)
        {
            changes = changes.Where(c => c.Risk == filter.Risk.Value);
        }

        if (filter.RequestedById.HasValue)
        {
            changes = changes.Where(c => c.RequestedById == filter.RequestedById.Value);
        }

        if (filter.AssignedToId.HasValue)
        {
            changes = changes.Where(c => c.AssignedToId == filter.AssignedToId.Value);
        }

        if (filter.AssignedToMe == true)
        {
            changes = changes.Where(c => c.AssignedToId == currentUserId);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        changes = query.SortBy?.ToLowerInvariant() switch
        {
            "planneddate" => descending ? changes.OrderByDescending(c => c.PlannedDate) : changes.OrderBy(c => c.PlannedDate),
            "risk" => descending ? changes.OrderByDescending(c => c.Risk) : changes.OrderBy(c => c.Risk),
            "status" => descending ? changes.OrderByDescending(c => c.Status) : changes.OrderBy(c => c.Status),
            _ => query.SortBy is null || descending
                ? changes.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                : changes.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
        };

        return await ToPagedResultAsync(Project(changes), query, cancellationToken);
    }

    public async Task<ChangeRequestDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(Set.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(cancellationToken);

    private IQueryable<ChangeRequestDto> Project(IQueryable<ChangeRequest> changes) =>
        changes.Select(c => new ChangeRequestDto(
            c.Id, c.Number, c.Title, c.Description, c.Type, c.Risk, c.Impact, c.Status,
            c.RequestedById,
            Context.Users.Where(u => u.Id == c.RequestedById).Select(u => u.FirstName + " " + u.LastName).First(),
            c.AssignedToId,
            Context.Users.Where(u => u.Id == c.AssignedToId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            c.DepartmentId, c.AssetId, c.PlannedDate, c.ImplementationDate, c.RollbackPlan,
            c.ApprovedById,
            Context.Users.Where(u => u.Id == c.ApprovedById).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            c.ApprovedAt, c.ReviewComment, c.RejectionReason, c.ImplementationNotes, c.CompletedAt, c.ClosedAt, c.CreatedAt));
}

public class PurchaseRequestRepository : BaseRepository<PurchaseRequest>, IPurchaseRequestRepository
{
    public PurchaseRequestRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<PurchaseRequest?> GetWithItemsAsync(int id, CancellationToken cancellationToken = default) =>
        await Set.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<PurchaseRequestDto>> SearchAsync(PagedQuery query, PurchaseFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var purchases = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            purchases = purchases.Where(p => p.Number.Contains(search) || p.Title.Contains(search));
        }

        if (filter.Status.HasValue)
        {
            purchases = purchases.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.RequestedById.HasValue)
        {
            purchases = purchases.Where(p => p.RequestedById == filter.RequestedById.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            purchases = purchases.Where(p => p.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.VendorId.HasValue)
        {
            purchases = purchases.Where(p => p.VendorId == filter.VendorId.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        purchases = query.SortBy?.ToLowerInvariant() switch
        {
            "estimatedcost" => descending ? purchases.OrderByDescending(p => p.EstimatedCost) : purchases.OrderBy(p => p.EstimatedCost),
            "neededdate" => descending ? purchases.OrderByDescending(p => p.NeededDate) : purchases.OrderBy(p => p.NeededDate),
            "status" => descending ? purchases.OrderByDescending(p => p.Status) : purchases.OrderBy(p => p.Status),
            _ => query.SortBy is null || descending
                ? purchases.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                : purchases.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
        };

        var total = await purchases.LongCountAsync(cancellationToken);
        var rows = await Project(purchases.Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new PagedResult<PurchaseRequestDto>(await CompleteAsync(rows, cancellationToken), query.Page, query.PageSize, total);
    }

    public async Task<PurchaseRequestDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default)
    {
        var rows = await Project(Set.AsNoTracking().Where(p => p.Id == id)).ToListAsync(cancellationToken);
        return (await CompleteAsync(rows, cancellationToken)).FirstOrDefault();
    }

    private IQueryable<PurchaseRequestDto> Project(IQueryable<PurchaseRequest> purchases) =>
        purchases.Select(p => new PurchaseRequestDto(
            p.Id, p.Number, p.Title, p.Description, p.Status, p.RequestedById,
            Context.Users.Where(u => u.Id == p.RequestedById).Select(u => u.FirstName + " " + u.LastName).First(),
            p.DepartmentId, p.VendorId, null, p.Justification, p.EstimatedCost, p.RequestedDate, p.NeededDate,
            p.ApprovedById, p.ApprovedAt, p.RejectionReason, p.OrderedAt, p.ReceivedAt, p.CancelledAt,
            Array.Empty<PurchaseItemDto>()));

    /// <summary>Agrega partidas y nombre del proveedor (incluidos eliminados) en consultas aparte.</summary>
    private async Task<IReadOnlyList<PurchaseRequestDto>> CompleteAsync(List<PurchaseRequestDto> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var items = await Context.PurchaseItems.AsNoTracking()
            .Where(i => ids.Contains(i.PurchaseRequestId))
            .OrderBy(i => i.Id)
            .Select(i => new { i.PurchaseRequestId, Dto = new PurchaseItemDto(i.Id, i.Description, i.Quantity, i.UnitPrice, i.TotalPrice, i.AssetTypeId, i.VendorId, i.Notes) })
            .ToListAsync(cancellationToken);
        var vendors = await VendorNameLookup.GetAsync(Context, rows.Select(r => r.VendorId), cancellationToken);

        return rows
            .Select(r => r with
            {
                VendorName = r.VendorId is { } v ? vendors.GetValueOrDefault(v) : null,
                Items = items.Where(i => i.PurchaseRequestId == r.Id).Select(i => i.Dto).ToList()
            })
            .ToList();
    }
}

/// <summary>Codigo y nombre de activos, incluidos dados de baja (ver nota de VendorNameLookup).</summary>
internal static class AssetLabelLookup
{
    public static async Task<Dictionary<int, (string? Code, string? Name)>> GetAsync(
        TIAdminDbContext context,
        IEnumerable<int> assetIds,
        CancellationToken cancellationToken)
    {
        var ids = assetIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Assets.IgnoreQueryFilters().AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => ((string?)a.AssetCode, (string?)a.Name), cancellationToken);
    }
}
