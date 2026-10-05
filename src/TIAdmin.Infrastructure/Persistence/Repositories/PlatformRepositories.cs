namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

public class DocumentRepository : BaseRepository<Document>, IDocumentRepository
{
    public DocumentRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DocumentDto>> GetByEntityAsync(string entityName, int entityId, CancellationToken cancellationToken = default) =>
        await Project(Set.AsNoTracking()
                .Where(d => d.EntityName == entityName && d.EntityId == entityId)
                .OrderByDescending(d => d.CreatedAt)) // ordenar antes de proyectar: el DTO no se traduce a SQL
            .ToListAsync(cancellationToken);

    public async Task<DocumentDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(Set.AsNoTracking().Where(d => d.Id == id)).FirstOrDefaultAsync(cancellationToken);

    private IQueryable<DocumentDto> Project(IQueryable<Document> documents) =>
        documents.Select(d => new DocumentDto(
            d.Id, d.FileName, d.MimeType, d.Size, d.Checksum, d.EntityName, d.EntityId, d.Description, d.Category,
            d.UploadedById,
            Context.Users.Where(u => u.Id == d.UploadedById).Select(u => u.FirstName + " " + u.LastName).First(),
            d.CreatedAt));
}

public sealed class NotificationRepository(TIAdminDbContext context) : INotificationRepository
{
    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default) =>
        await context.Notifications.AddRangeAsync(notifications, cancellationToken);

    public async Task<HashSet<(int UserId, string DedupKey)>> GetExistingDedupKeysAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var rows = await context.Notifications.AsNoTracking()
            .Where(n => n.DedupKey != null && keys.Contains(n.DedupKey))
            .Select(n => new { n.UserId, n.DedupKey })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.UserId, r.DedupKey!)).ToHashSet();
    }

    public async Task<PagedResult<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly, PagedQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var notifications = context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id);

        var total = await notifications.LongCountAsync(cancellationToken);
        var items = await notifications
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.Link, n.EntityName, n.EntityId, n.IsRead, n.ReadAt, n.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<Notification?> GetForUserAsync(int userId, int notificationId, CancellationToken cancellationToken = default) =>
        await context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

    public async Task<int> MarkAllReadAsync(int userId, DateTime now, CancellationToken cancellationToken = default)
    {
        var unread = await context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        return unread.Count;
    }

    public void Discard(IEnumerable<Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            context.Entry(notification).State = EntityState.Detached;
        }
    }
}

public sealed class ExportJobRepository(TIAdminDbContext context) : IExportJobRepository
{
    public async Task AddAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        await context.ExportJobs.AddAsync(job, cancellationToken);

    public async Task<ExportJob?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await context.ExportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<int>> GetPendingIdsAsync(CancellationToken cancellationToken = default) =>
        await context.ExportJobs.AsNoTracking()
            .Where(j => j.Status == ExportJobStatus.Pending)
            .OrderBy(j => j.Id)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);
}
