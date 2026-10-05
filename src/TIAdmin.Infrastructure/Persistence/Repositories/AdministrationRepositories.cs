namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

/// <summary>Bitacora de auditoria: solo lectura.</summary>
public sealed class AuditLogRepository(TIAdminDbContext context) : IAuditLogRepository
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, AuditFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var logs = context.AuditLogs.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            logs = logs.Where(a => a.EntityName.Contains(search) || a.EntityId.Contains(search)
                || (a.UserName != null && a.UserName.Contains(search)));
        }

        if (filter.UserId.HasValue)
        {
            logs = logs.Where(a => a.UserId == filter.UserId.Value);
        }

        if (filter.Action.HasValue)
        {
            logs = logs.Where(a => a.Action == filter.Action.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            logs = logs.Where(a => a.Module == filter.Module);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            logs = logs.Where(a => a.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            logs = logs.Where(a => a.EntityId == filter.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.IpAddress))
        {
            logs = logs.Where(a => a.IpAddress == filter.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            logs = logs.Where(a => a.CorrelationId == filter.CorrelationId);
        }

        if (filter.From.HasValue)
        {
            logs = logs.Where(a => a.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            logs = logs.Where(a => a.Timestamp <= filter.To.Value);
        }

        // Cronologico inverso por defecto; el Id rompe empates dentro del mismo instante.
        logs = query.SortDirection == SortDirection.Ascending && query.SortBy is not null
            ? logs.OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            : logs.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id);

        var total = await logs.LongCountAsync(cancellationToken);
        var items = await Project(logs.Skip(query.Skip).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new PagedResult<AuditLogDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AuditLogDto?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        await Project(context.AuditLogs.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<AuditLogDto> Project(IQueryable<AuditLog> logs) =>
        logs.Select(a => new AuditLogDto(a.Id, a.Timestamp, a.UserId, a.UserName, a.Action, a.Module, a.EntityName, a.EntityId,
            a.OldValues, a.NewValues, a.IpAddress, a.UserAgent, a.CorrelationId));
}

public sealed class SystemConfigurationRepository(TIAdminDbContext context) : ISystemConfigurationRepository
{
    public async Task<IReadOnlyList<SystemConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.SystemConfigurations.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<SystemConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        await context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
}
