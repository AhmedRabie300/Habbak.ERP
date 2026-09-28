using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Notifications.Commands;
using Habbak.ERP.Application.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Notifications;

/// <summary>
/// /notifications — Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5. Personal, per-signed-in-user
/// data (a user's own notifications), not a screen with role-based permissions — same precedent as
/// AttachmentsController/FieldLabelsController (Docs/Implementation/Phase-2.5-Research.md §1.7):
/// no [Screen] gate, no MenuItem registration needed. Every command/query here scopes to the
/// caller's own UserId server-side regardless of what the client asks for.
/// </summary>
[ApiController]
[Authorize]
[AnySignedInUser]
[Route("api/v1/notifications")]
public class NotificationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] NotificationFilter filter = NotificationFilter.All, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetMyNotificationsQuery(filter, page, pageSize), cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken) =>
        Ok(new { count = await mediator.Send(new GetUnreadCountQuery(), cancellationToken) });

    [HttpGet("pending-action-count")]
    public async Task<IActionResult> GetPendingActionCount(CancellationToken cancellationToken) =>
        Ok(new { count = await mediator.Send(new GetPendingActionCountQuery(), cancellationToken) });

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkNotificationAsReadCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkAllNotificationsAsReadCommand(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteNotificationCommand(id), cancellationToken);
        return NoContent();
    }
}
