namespace TIAdmin.Application.Platform;

using System.Net;
using Microsoft.Extensions.Logging;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Notificacion a uno o mas usuarios. <paramref name="DedupKey"/> evita repetir alertas periodicas;
/// <paramref name="ExcludeUserId"/> omite a quien provoco el evento.
/// </summary>
public record NotificationRequest(
    IReadOnlyCollection<int> UserIds,
    string Type,
    string Title,
    string Message,
    string? EntityName = null,
    int? EntityId = null,
    string? Link = null,
    string? DedupKey = null,
    int? ExcludeUserId = null,
    bool SendEmail = true);

/// <summary>
/// Notificaciones internas + correo (SPECS.md seccion 40). Enviar nunca hace fallar la operacion de
/// negocio que lo provoco: los errores se registran y se descartan (ADR-035).
/// </summary>
public interface INotificationService
{
    /// <summary>Devuelve cuantas notificaciones se crearon.</summary>
    Task<int> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Notifica a todos los usuarios activos con el permiso.</summary>
    Task<int> NotifyPermissionAsync(string permission, NotificationRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationDto>> GetMineAsync(bool unreadOnly, PagedQuery query, CancellationToken cancellationToken = default);

    Task<int> CountMyUnreadAsync(CancellationToken cancellationToken = default);

    Task MarkReadAsync(int notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);
}

public sealed class NotificationService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    IEmailQueue emailQueue,
    ICurrentUserService currentUser,
    IClock clock,
    ILogger<NotificationService> logger)
    : INotificationService
{
    public async Task<int> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<Notification> pending = [];

        try
        {
            var ids = request.UserIds.Where(id => id != request.ExcludeUserId).Distinct().ToList();
            var recipients = await userDirectory.FindActiveAsync(ids, cancellationToken);
            if (recipients.Count == 0)
            {
                return 0;
            }

            if (request.DedupKey is { } key)
            {
                var existing = await unitOfWork.Notifications.GetExistingDedupKeysAsync([key], cancellationToken);
                recipients = recipients.Where(r => !existing.Contains((r.Id, key))).ToList();
                if (recipients.Count == 0)
                {
                    return 0;
                }
            }

            var now = clock.UtcNow;
            pending = recipients.Select(r => new Notification
            {
                UserId = r.Id,
                Type = request.Type,
                Title = Truncate(request.Title, 200),
                Message = Truncate(request.Message, 2000),
                Link = request.Link,
                EntityName = request.EntityName,
                EntityId = request.EntityId,
                DedupKey = request.DedupKey,
                CreatedAt = now
            }).ToList();

            await unitOfWork.Notifications.AddRangeAsync(pending, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (request.SendEmail)
            {
                var emails = recipients.Select(r => r.Email).OfType<string>().Where(e => e.Length > 0).ToList();
                if (emails.Count > 0)
                {
                    emailQueue.Enqueue(new EmailMessage(emails, request.Title, Html(request)));
                }
            }

            return pending.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Una carrera de deduplicacion u otro fallo no debe deshacer la operacion que ya se guardo.
            unitOfWork.Notifications.Discard(pending);
            logger.LogWarning(exception, "No se pudo registrar la notificacion {Type} ({EntityName} {EntityId}).",
                request.Type, request.EntityName, request.EntityId);
            return 0;
        }
    }

    public async Task<int> NotifyPermissionAsync(string permission, NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var users = await userDirectory.FindActiveWithPermissionAsync(permission, cancellationToken);
        return await NotifyAsync(request with { UserIds = users }, cancellationToken);
    }

    public Task<PagedResult<NotificationDto>> GetMineAsync(bool unreadOnly, PagedQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Notifications.GetForUserAsync(Me, unreadOnly, query, cancellationToken);

    public Task<int> CountMyUnreadAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.Notifications.CountUnreadAsync(Me, cancellationToken);

    public async Task MarkReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await unitOfWork.Notifications.GetForUserAsync(Me, notificationId, cancellationToken)
            ?? throw new EntityNotFoundException("Notification", notificationId);

        notification.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var count = await unitOfWork.Notifications.MarkAllReadAsync(Me, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return count;
    }

    private int Me => currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private static string Html(NotificationRequest request)
    {
        var link = request.Link is null ? string.Empty : $"<p>Ver: {WebUtility.HtmlEncode(request.Link)}</p>";
        return $"""
            <h2 style="font-family:sans-serif">{WebUtility.HtmlEncode(request.Title)}</h2>
            <p style="font-family:sans-serif">{WebUtility.HtmlEncode(request.Message)}</p>
            {link}
            <p style="font-family:sans-serif;color:#888;font-size:12px">TI Admin - mensaje automatico, no responder.</p>
            """;
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
