namespace TIAdmin.Infrastructure.Reporting;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Enums;
using TIAdmin.Infrastructure.Persistence;

/// <summary>
/// Agregados para dashboard y reportes. Los conteos y sumas se calculan en SQL; los nombres se
/// resuelven en consultas aparte (sin navegaciones a principales con soft delete).
/// </summary>
public sealed class ReportingQueries(TIAdminDbContext context) : IReportingQueries
{
    private const int TopGroups = 10;

    private static readonly TicketStatus[] ClosedTicketStatuses = [TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Cancelled];

    public async Task<MyKpis> GetMyKpisAsync(int userId, CancellationToken cancellationToken = default)
    {
        var openTickets = await context.Tickets.CountAsync(
            t => t.RequesterId == userId && !ClosedTicketStatuses.Contains(t.Status), cancellationToken);
        var assets = await context.Assets.CountAsync(a => a.CurrentUserId == userId, cancellationToken);
        var assignedToMe = await context.Tickets.CountAsync(
            t => t.AssignedToId == userId && !ClosedTicketStatuses.Contains(t.Status), cancellationToken);

        return new MyKpis(openTickets, assets, assignedToMe);
    }

    public async Task<AssetKpis> GetAssetKpisAsync(CancellationToken cancellationToken = default)
    {
        var total = await context.Assets.CountAsync(cancellationToken);
        var assigned = await context.Assets.CountAsync(a => a.Status == AssetStatus.Assigned, cancellationToken);

        return new AssetKpis(total, assigned,
            await AssetsByStatusAsync(cancellationToken),
            await AssetsByDepartmentAsync(cancellationToken),
            await AssetsByLocationAsync(cancellationToken));
    }

    public async Task<TicketKpis> GetTicketKpisAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var open = context.Tickets.Where(t => !ClosedTicketStatuses.Contains(t.Status));

        var openCount = await open.CountAsync(cancellationToken);
        var overdue = await open.CountAsync(
            t => (t.FirstResponseAt == null && t.DueAtResponse < now) || t.DueAtResolution < now, cancellationToken);
        var createdLast7Days = await context.Tickets.CountAsync(t => t.CreatedAt >= now.AddDays(-7), cancellationToken);

        var byPriority = (await open.GroupBy(t => t.Priority)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .OrderByDescending(g => g.Key)
            .Select(g => new CountByLabel(g.Key.ToString(), g.Count))
            .ToList();

        var since = now.AddDays(-30);
        var resolved = await context.Tickets.CountAsync(t => t.ResolvedAt >= since && t.DueAtResolution != null, cancellationToken);
        var withinSla = await context.Tickets.CountAsync(
            t => t.ResolvedAt >= since && t.DueAtResolution != null && !t.IsSlaBreached, cancellationToken);

        return new TicketKpis(openCount, overdue, createdLast7Days, byPriority, Percent(withinSla, resolved));
    }

    public async Task<LicenseKpis> GetLicenseKpisAsync(DateOnly today, DateOnly expiringUntil, CancellationToken cancellationToken = default)
    {
        var active = context.SoftwareLicenses.Where(l => l.IsActive);

        var total = await active.CountAsync(cancellationToken);
        var expired = await active.CountAsync(l => l.ExpirationDate < today, cancellationToken);
        var expiring = await active.CountAsync(l => l.ExpirationDate >= today && l.ExpirationDate <= expiringUntil, cancellationToken);

        return new LicenseKpis(total, expiring, expired, Percent(total - expired, total));
    }

    public async Task<ContractKpis> GetContractKpisAsync(DateOnly today, DateOnly expiringUntil, CancellationToken cancellationToken = default)
    {
        var active = context.Contracts.Where(c => c.Status == ContractStatus.Active);

        return new ContractKpis(
            await active.CountAsync(c => c.EndDate >= today, cancellationToken),
            await active.CountAsync(c => c.EndDate >= today && c.EndDate <= expiringUntil, cancellationToken),
            await active.CountAsync(c => c.EndDate < today, cancellationToken));
    }

    public async Task<MaintenanceKpis> GetMaintenanceKpisAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var pending = context.Maintenances.Where(m => m.Status == MaintenanceStatus.Planned || m.Status == MaintenanceStatus.Scheduled);

        return new MaintenanceKpis(
            await pending.CountAsync(cancellationToken),
            await pending.CountAsync(m => m.ScheduledDate < now, cancellationToken),
            await context.Maintenances.CountAsync(m => m.Status == MaintenanceStatus.InProgress, cancellationToken));
    }

    public async Task<CostReport> GetCostReportAsync(DateTime from, DateTime to, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        // Compras: se reconocen al ordenarse (Ordered o Received).
        var purchases = await context.PurchaseRequests
            .Where(p => (p.Status == PurchaseStatus.Ordered || p.Status == PurchaseStatus.Received)
                && p.OrderedAt >= from && p.OrderedAt < to)
            .Select(p => new { Date = p.OrderedAt!.Value, Amount = p.EstimatedCost })
            .ToListAsync(cancellationToken);

        var maintenance = await context.Maintenances
            .Where(m => m.Status == MaintenanceStatus.Completed && m.CompletedAt >= from && m.CompletedAt < to && m.ActualCost != null)
            .Select(m => new { Date = m.CompletedAt!.Value, Amount = m.ActualCost!.Value })
            .ToListAsync(cancellationToken);

        // Licencias: por fecha de compra (fecha de negocio).
        var fromDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(from, timeZone));
        var toDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(to, timeZone));
        var licenses = await context.SoftwareLicenses
            .Where(l => l.Cost != null && l.PurchaseDate >= fromDate && l.PurchaseDate < toDate)
            .Select(l => new { Date = l.PurchaseDate!.Value, Amount = l.Cost!.Value })
            .ToListAsync(cancellationToken);

        var buckets = new SortedDictionary<(int Year, int Month), decimal[]>();
        void Add((int Year, int Month) key, int index, decimal amount)
        {
            if (!buckets.TryGetValue(key, out var values))
            {
                values = new decimal[3];
                buckets[key] = values;
            }

            values[index] += amount;
        }

        foreach (var p in purchases)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(p.Date, timeZone);
            Add((local.Year, local.Month), 0, p.Amount);
        }

        foreach (var m in maintenance)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(m.Date, timeZone);
            Add((local.Year, local.Month), 1, m.Amount);
        }

        foreach (var l in licenses)
        {
            Add((l.Date.Year, l.Date.Month), 2, l.Amount);
        }

        var byMonth = buckets
            .Select(b => new CostByMonth(b.Key.Year, b.Key.Month, b.Value[0], b.Value[1], b.Value[2], b.Value.Sum()))
            .ToList();

        var totalPurchases = purchases.Sum(p => p.Amount);
        var totalMaintenance = maintenance.Sum(m => m.Amount);
        var totalLicenses = licenses.Sum(l => l.Amount);
        return new CostReport(from, to, totalPurchases, totalMaintenance, totalLicenses,
            totalPurchases + totalMaintenance + totalLicenses, byMonth);
    }

    public async Task<AssetSummaryReport> GetAssetSummaryAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var total = await context.Assets.CountAsync(cancellationToken);
        var cost = await context.Assets.SumAsync(a => a.PurchaseCost ?? 0, cancellationToken);
        var warrantyUntil = today.AddDays(90);
        var warrantyExpiring = await context.Assets.CountAsync(
            a => a.WarrantyExpiration >= today && a.WarrantyExpiration <= warrantyUntil, cancellationToken);

        var byTypeRows = await context.Assets.GroupBy(a => a.AssetTypeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var typeNames = await context.AssetTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
        var byType = byTypeRows
            .OrderByDescending(r => r.Count)
            .Select(r => new CountByLabel(typeNames.GetValueOrDefault(r.Key, $"Tipo {r.Key}"), r.Count))
            .ToList();

        return new AssetSummaryReport(total, cost,
            await AssetsByStatusAsync(cancellationToken),
            byType,
            await AssetsByDepartmentAsync(cancellationToken, top: int.MaxValue),
            await AssetsByLocationAsync(cancellationToken, top: int.MaxValue),
            warrantyExpiring);
    }

    public async Task<IReadOnlyList<AssetsByUserRow>> GetAssetsByUserAsync(int? departmentId, CancellationToken cancellationToken = default)
    {
        var assets = await context.Assets.AsNoTracking()
            .Where(a => a.CurrentUserId != null)
            .Select(a => new { UserId = a.CurrentUserId!.Value, a.AssetCode })
            .ToListAsync(cancellationToken);

        var userIds = assets.Select(a => a.UserId).Distinct().ToList();
        var users = await context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && (departmentId == null || u.DepartmentId == departmentId))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return users
            .Select(u =>
            {
                var codes = assets.Where(a => a.UserId == u.Id).Select(a => a.AssetCode).Order().ToList();
                return new AssetsByUserRow(u.Id, u.Name, u.Email, codes.Count, codes);
            })
            .OrderByDescending(r => r.AssetCount)
            .ThenBy(r => r.UserName)
            .ToList();
    }

    public async Task<TicketReport> GetTicketReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tickets = context.Tickets.Where(t => t.CreatedAt >= from && t.CreatedAt < to);

        var created = await tickets.CountAsync(cancellationToken);
        var resolved = await tickets.CountAsync(t => t.ResolvedAt != null, cancellationToken);
        var open = await tickets.CountAsync(t => !ClosedTicketStatuses.Contains(t.Status), cancellationToken);

        var byStatus = await GroupEnumAsync(tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }), r => r.Key.ToString(), r => r.Count, cancellationToken);
        var byPriority = await GroupEnumAsync(tickets.GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }), r => r.Key.ToString(), r => r.Count, cancellationToken);
        var byType = await GroupEnumAsync(tickets.GroupBy(t => t.Type).Select(g => new { g.Key, Count = g.Count() }), r => r.Key.ToString(), r => r.Count, cancellationToken);

        var categoryRows = await tickets.GroupBy(t => t.CategoryId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var categoryNames = await context.TicketCategories.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var byCategory = categoryRows
            .OrderByDescending(r => r.Count)
            .Select(r => new CountByLabel(categoryNames.GetValueOrDefault(r.Key, $"Categoria {r.Key}"), r.Count))
            .ToList();

        var technicianRows = await tickets.GroupBy(t => t.AssignedToId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var technicianNames = await UserNamesAsync(technicianRows.Select(r => r.Key), cancellationToken);
        var byTechnician = technicianRows
            .OrderByDescending(r => r.Count)
            .Select(r => new CountByLabel(r.Key is { } id ? technicianNames.GetValueOrDefault(id, $"Usuario {id}") : "Sin asignar", r.Count))
            .ToList();

        // Promedio en memoria: DATEDIFF es especifico de SQL Server y los tests corren en InMemory.
        var durations = await tickets.Where(t => t.ResolvedAt != null)
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value })
            .ToListAsync(cancellationToken);
        decimal? averageHours = durations.Count == 0
            ? null
            : Math.Round((decimal)durations.Average(d => (d.ResolvedAt - d.CreatedAt).TotalHours), 1);

        return new TicketReport(from, to, created, resolved, open, byStatus, byPriority, byCategory, byTechnician, byType, averageHours);
    }

    public async Task<SlaReport> GetSlaReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var rows = await context.Tickets.AsNoTracking()
            .Where(t => t.ResolvedAt >= from && t.ResolvedAt < to && t.DueAtResolution != null)
            .Select(t => new { t.Priority, t.CategoryId, t.IsSlaBreached })
            .ToListAsync(cancellationToken);

        var categoryNames = await context.TicketCategories.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var byPriority = rows.GroupBy(r => r.Priority)
            .OrderByDescending(g => g.Key)
            .Select(g => Compliance(g.Key.ToString(), g.Count(), g.Count(r => !r.IsSlaBreached)))
            .ToList();
        var byCategory = rows.GroupBy(r => r.CategoryId)
            .Select(g => Compliance(categoryNames.GetValueOrDefault(g.Key, $"Categoria {g.Key}"), g.Count(), g.Count(r => !r.IsSlaBreached)))
            .OrderBy(r => r.CompliancePercent)
            .ToList();

        var within = rows.Count(r => !r.IsSlaBreached);
        return new SlaReport(from, to, rows.Count, within, Percent(within, rows.Count), byPriority, byCategory);
    }

    public async Task<IReadOnlyList<LicenseUsageReportRow>> GetLicenseUsageAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var rows = await context.SoftwareLicenses.AsNoTracking()
            .Where(l => l.IsActive)
            .GroupBy(l => l.SoftwareId)
            .Select(g => new
            {
                SoftwareId = g.Key,
                Licenses = g.Count(),
                Seats = g.Sum(l => l.Quantity),
                Used = g.Sum(l => l.UsedQuantity),
                Expired = g.Count(l => l.ExpirationDate < today),
                NextExpiration = g.Where(l => l.ExpirationDate >= today).Min(l => l.ExpirationDate)
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.SoftwareId).ToList();
        var names = await context.Softwares.IgnoreQueryFilters().AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Version == null ? s.Name : s.Name + " " + s.Version, cancellationToken);

        return rows
            .Select(r => new LicenseUsageReportRow(r.SoftwareId, names.GetValueOrDefault(r.SoftwareId, $"Software {r.SoftwareId}"),
                r.Licenses, r.Seats, r.Used, Percent(r.Used, r.Seats), r.Expired, r.NextExpiration))
            .OrderBy(r => r.Software)
            .ToList();
    }

    private async Task<IReadOnlyList<CountByLabel>> AssetsByStatusAsync(CancellationToken cancellationToken) =>
        await GroupEnumAsync(context.Assets.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }),
            r => r.Key.ToString(), r => r.Count, cancellationToken);

    private async Task<IReadOnlyList<CountByLabel>> AssetsByDepartmentAsync(CancellationToken cancellationToken, int top = TopGroups)
    {
        var rows = await context.Assets.GroupBy(a => a.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var names = await context.Departments.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        return rows
            .OrderByDescending(r => r.Count)
            .Take(top)
            .Select(r => new CountByLabel(r.Key is { } id ? names.GetValueOrDefault(id, $"Departamento {id}") : "Sin departamento", r.Count))
            .ToList();
    }

    private async Task<IReadOnlyList<CountByLabel>> AssetsByLocationAsync(CancellationToken cancellationToken, int top = TopGroups)
    {
        var rows = await context.Assets.GroupBy(a => a.LocationId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var names = await context.Locations.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);
        return rows
            .OrderByDescending(r => r.Count)
            .Take(top)
            .Select(r => new CountByLabel(r.Key is { } id ? names.GetValueOrDefault(id, $"Ubicacion {id}") : "Sin ubicacion", r.Count))
            .ToList();
    }

    private static async Task<IReadOnlyList<CountByLabel>> GroupEnumAsync<T>(
        IQueryable<T> grouped,
        Func<T, string> label,
        Func<T, int> count,
        CancellationToken cancellationToken) =>
        (await grouped.ToListAsync(cancellationToken))
            .Select(r => new CountByLabel(label(r), count(r)))
            .OrderByDescending(r => r.Count)
            .ToList();

    private async Task<Dictionary<int, string>> UserNamesAsync(IEnumerable<int?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.OfType<int>().Distinct().ToList();
        return await context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, cancellationToken);
    }

    private static SlaComplianceRow Compliance(string label, int resolved, int within) =>
        new(label, resolved, within, Percent(within, resolved));

    private static decimal? Percent(int part, int total) =>
        total == 0 ? null : Math.Round(part * 100m / total, 1);
}
