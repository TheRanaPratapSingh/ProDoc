using Microsoft.AspNetCore.Mvc;
using ProDoc.Api.Auth;
using ProDoc.Application.Notifications;
using ProDoc.Domain.Identity;

namespace ProDoc.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HasPermission(PermissionCodes.AccountRead)]
    [HttpGet("me")]
    public Task<IReadOnlyList<NotificationInboxItem>> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default) =>
        notifications.ListMineAsync(User.GetUserId(), unreadOnly, skip, take, cancellationToken);

    [HasPermission(PermissionCodes.AccountRead)]
    [HttpGet("me/unread-count")]
    public Task<NotificationUnreadCount> UnreadCount(CancellationToken cancellationToken) =>
        notifications.UnreadCountAsync(User.GetUserId(), cancellationToken);

    [HasPermission(PermissionCodes.AccountWrite)]
    [HttpPost("me/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(User.GetUserId(), notificationId, cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AccountWrite)]
    [HttpPost("me/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HasPermission(PermissionCodes.AccountWrite)]
    [HttpDelete("me/{notificationId:guid}")]
    public async Task<IActionResult> Delete(Guid notificationId, CancellationToken cancellationToken)
    {
        await notifications.DeleteAsync(User.GetUserId(), notificationId, cancellationToken);
        return NoContent();
    }
}
