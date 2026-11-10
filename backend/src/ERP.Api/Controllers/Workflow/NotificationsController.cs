using ERP.Application.Workflow;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Workflow;

/// <summary>The caller's in-app notifications.</summary>
[Route("api/v1/notifications")]
public sealed class NotificationsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [Authorize]
    public Task<NotificationList> List(CancellationToken ct)
        => Mediator.Send(new ListNotificationsQuery(), ct);

    [HttpPost("read-all")]
    [Authorize]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await Mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new MarkNotificationReadCommand(id), ct);
        return NoContent();
    }
}
