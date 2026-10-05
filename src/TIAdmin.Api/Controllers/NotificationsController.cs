namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Platform;

/// <summary>Notificaciones internas del usuario autenticado (cada usuario solo ve las suyas).</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetMine(
        [FromQuery] PagedQuery query,
        [FromQuery] bool unreadOnly,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(await notifications.GetMineAsync(unreadOnly, query, cancellationToken)));

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(ApiResponse<int>.Ok(await notifications.CountMyUnreadAsync(cancellationToken)));

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<ApiResponse>> MarkRead(int id, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead(CancellationToken cancellationToken) =>
        Ok(ApiResponse<int>.Ok(await notifications.MarkAllReadAsync(cancellationToken)));
}
