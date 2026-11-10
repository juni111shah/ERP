using ERP.Application.Common;
using ERP.Application.Reminders;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Workflow;

/// <summary>The outbound message log, a test email and running the reminders now.</summary>
[Route("api/v1/messages")]
public sealed class MessagesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<PagedResult<MessageLogDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, MessageLogDto.Projection, CorePermissions.SettingsRead, ct);

    [HttpPost("test-email")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<MessageLogDto> SendTestEmail(TestEmailRequest r, CancellationToken ct)
        => Mediator.Send(new SendTestEmailCommand(r.To), ct);

    [HttpPost("send-reminders")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public async Task<object> SendReminders(CancellationToken ct)
        => new { sent = await Mediator.Send(new SendRemindersNowCommand(), ct) };

    /// <summary>Where to send the test email.</summary>
    public sealed record TestEmailRequest(string To);
}
