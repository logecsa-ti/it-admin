namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public class TicketCategoryRepository : BaseRepository<TicketCategory>, ITicketCategoryRepository
{
    public TicketCategoryRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<TicketCategoryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(c => isActive == null || c.IsActive == isActive)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .Select(c => new TicketCategoryDto(c.Id, c.Code, c.Name, c.Description, c.Type, c.DepartmentId, c.DefaultPriority,
                c.RequiresApproval, c.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().AnyAsync(c => c.Code == code, cancellationToken);

    public async Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(c => c.Name == name && (excludeId == null || c.Id != excludeId), cancellationToken);
}

public class SlaPolicyRepository : BaseRepository<SlaPolicy>, ISlaPolicyRepository
{
    public SlaPolicyRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<SlaPolicy>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().Where(p => p.IsActive).ToListAsync(cancellationToken);

    public async Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(p => p.Name == name && (excludeId == null || p.Id != excludeId), cancellationToken);

    public async Task ClearDefaultAsync(int exceptId, CancellationToken cancellationToken = default)
    {
        var others = await Set.Where(p => p.IsDefault && p.Id != exceptId).ToListAsync(cancellationToken);
        foreach (var policy in others)
        {
            policy.IsDefault = false;
        }
    }
}

public class TicketRepository : BaseRepository<Ticket>, ITicketRepository
{
    public TicketRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<TicketListItemDto>> SearchAsync(
        PagedQuery query,
        TicketFilter filter,
        TicketScope scope,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        // ADR-026: sin TICKETS.VIEW / REQUESTS.VIEW cada usuario solo ve lo que solicito.
        var tickets = Set.AsNoTracking().Where(t =>
            (t.Type == TicketType.Incident && scope.AllIncidents)
            || (t.Type == TicketType.ServiceRequest && scope.AllServiceRequests)
            || t.RequesterId == scope.UserId);

        if (query.NormalizeSearch() is { } search)
        {
            tickets = tickets.Where(t => t.TicketNumber.Contains(search) || t.Title.Contains(search));
        }

        if (filter.Type.HasValue)
        {
            tickets = tickets.Where(t => t.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            tickets = tickets.Where(t => t.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            tickets = tickets.Where(t => t.Priority == filter.Priority.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            tickets = tickets.Where(t => t.CategoryId == filter.CategoryId.Value);
        }

        if (filter.AssignedToId.HasValue)
        {
            tickets = tickets.Where(t => t.AssignedToId == filter.AssignedToId.Value);
        }

        if (filter.RequesterId.HasValue)
        {
            tickets = tickets.Where(t => t.RequesterId == filter.RequesterId.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            tickets = tickets.Where(t => t.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.Mine == true)
        {
            tickets = tickets.Where(t => t.RequesterId == scope.UserId);
        }

        if (filter.AssignedToMe == true)
        {
            tickets = tickets.Where(t => t.AssignedToId == scope.UserId);
        }

        if (filter.Overdue == true)
        {
            tickets = tickets.Where(t =>
                t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled
                && ((t.FirstResponseAt == null && t.DueAtResponse < now) || t.DueAtResolution < now));
        }

        var descending = query.SortDirection == SortDirection.Descending;
        tickets = query.SortBy?.ToLowerInvariant() switch
        {
            "priority" => descending ? tickets.OrderByDescending(t => t.Priority) : tickets.OrderBy(t => t.Priority),
            "status" => descending ? tickets.OrderByDescending(t => t.Status) : tickets.OrderBy(t => t.Status),
            "dueatresolution" => descending ? tickets.OrderByDescending(t => t.DueAtResolution) : tickets.OrderBy(t => t.DueAtResolution),
            "number" => descending ? tickets.OrderByDescending(t => t.Id) : tickets.OrderBy(t => t.Id),
            // Por defecto: lo mas reciente primero.
            _ => query.SortBy is null || descending
                ? tickets.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                : tickets.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
        };

        var total = await tickets.LongCountAsync(cancellationToken);
        var rows = await tickets
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Type,
                t.Title,
                t.CategoryId,
                t.Priority,
                t.Status,
                t.RequesterId,
                RequesterName = Context.Users.Where(u => u.Id == t.RequesterId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                t.AssignedToId,
                AssignedToName = Context.Users.Where(u => u.Id == t.AssignedToId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                t.ApprovalStatus,
                t.CreatedAt,
                t.DueAtResponse,
                t.DueAtResolution,
                t.FirstResponseAt,
                t.ResolvedAt
            })
            .ToListAsync(cancellationToken);

        var categories = await CategoryNamesAsync(rows.Select(r => r.CategoryId), cancellationToken);
        var items = rows
            .Select(r => new TicketListItemDto(
                r.Id, r.TicketNumber, r.Type, r.Title, r.CategoryId, categories.GetValueOrDefault(r.CategoryId, string.Empty),
                r.Priority, r.Status, r.RequesterId, r.RequesterName ?? string.Empty, r.AssignedToId, r.AssignedToName,
                r.ApprovalStatus, r.CreatedAt, Sla(r.DueAtResponse, r.DueAtResolution, r.FirstResponseAt, r.ResolvedAt, now)))
            .ToList();

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<TicketDetailDto?> GetDetailAsync(int id, DateTime now, CancellationToken cancellationToken = default)
    {
        var row = await Set.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Ticket = t,
                RequesterName = Context.Users.Where(u => u.Id == t.RequesterId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                AssignedToName = Context.Users.Where(u => u.Id == t.AssignedToId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                AssetCode = Context.Assets.Where(a => a.Id == t.AssetId).Select(a => a.AssetCode).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var t = row.Ticket;
        var categories = await CategoryNamesAsync([t.CategoryId], cancellationToken);

        return new TicketDetailDto(
            t.Id, t.TicketNumber, t.Type, t.Title, t.Description, t.CategoryId, categories.GetValueOrDefault(t.CategoryId, string.Empty),
            t.Priority, t.Status, t.RequesterId, row.RequesterName ?? string.Empty, t.AssignedToId, row.AssignedToName,
            t.DepartmentId, t.AssetId, row.AssetCode, t.NeededByDate, t.ResolutionNotes, t.ResolvedAt, t.ClosedAt,
            t.ApprovalStatus, t.ApprovedById, t.ApprovedAt, t.ApprovalComment, t.SlaPolicyId, t.CreatedAt, t.UpdatedAt,
            Sla(t.DueAtResponse, t.DueAtResolution, t.FirstResponseAt, t.ResolvedAt, now));
    }

    public async Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default) =>
        await Context.TicketComments.AddAsync(comment, cancellationToken);

    public async Task AddHistoryAsync(TicketStatusHistory history, CancellationToken cancellationToken = default) =>
        await Context.TicketStatusHistory.AddAsync(history, cancellationToken);

    public async Task<IReadOnlyList<TicketCommentDto>> GetCommentsAsync(int ticketId, bool includeInternal, CancellationToken cancellationToken = default) =>
        await Context.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticketId && (includeInternal || !c.IsInternal))
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new TicketCommentDto(
                c.Id,
                c.UserId,
                Context.Users.Where(u => u.Id == c.UserId).Select(u => u.FirstName + " " + u.LastName).First(),
                c.Content,
                c.IsInternal,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketHistoryDto>> GetHistoryAsync(int ticketId, CancellationToken cancellationToken = default) =>
        await Context.TicketStatusHistory.AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.Timestamp)
            .ThenBy(h => h.Id)
            .Select(h => new TicketHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.UserName, h.Comment, h.Timestamp))
            .ToListAsync(cancellationToken);

    /// <summary>Estado del SLA al momento de la consulta.</summary>
    private static TicketSlaDto Sla(DateTime? dueResponse, DateTime? dueResolution, DateTime? firstResponse, DateTime? resolved, DateTime now)
    {
        var responseBreached = dueResponse is { } r && (firstResponse ?? now) > r;
        var resolutionBreached = dueResolution is { } d && (resolved ?? now) > d;
        return new TicketSlaDto(dueResponse, dueResolution, firstResponse, responseBreached, resolutionBreached,
            responseBreached || resolutionBreached);
    }

    /// <summary>
    /// Nombres de categoria (incluidas eliminadas) en consulta aparte: ver la nota de VendorNameLookup
    /// sobre IgnoreQueryFilters y navegaciones requeridas con soft delete.
    /// </summary>
    private async Task<Dictionary<int, string>> CategoryNamesAsync(IEnumerable<int> categoryIds, CancellationToken cancellationToken)
    {
        var ids = categoryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await Context.TicketCategories.IgnoreQueryFilters().AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }
}
