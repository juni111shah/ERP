using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Services.Controllers;

/// <summary>Time logged on engagements.</summary>
[ModuleGate(ServicesModule.Key)]
[Route("api/v1/m/services/time-entries")]
public sealed class TimeEntriesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.TimeEntriesRead)]
    public Task<PagedResult<TimeEntrySummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, TimeEntrySummaryDto.Projection, Permissions.TimeEntriesRead, ct);

    [HttpPost]
    [RequirePermission(Permissions.TimeEntriesManage)]
    public async Task<IActionResult> Log(LogTimeEntryCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/services/time-entries/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.TimeEntriesManage)]
    public Task<TimeEntryDto> Update(Guid id, UpdateTimeEntryRequest body, CancellationToken ct)
        => Mediator.Send(new UpdateTimeEntryCommand(id, body.ActivityDate, body.Hours, body.HourlyRate, body.TaskDescription, body.IsBillable, body.ConsultantUserId, body.ConsultantName, body.CustomFields), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.TimeEntriesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteTimeEntryCommand(id), ct);
        return NoContent();
    }

    // The approver is the signed-in user. A body is optional; what older clients send in it (id, approvedByUserId) changes nothing except that a mismatched id is refused.
    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permissions.TimeEntriesManage)]
    public async Task<IActionResult> Approve(Guid id, ApproveTimeEntryRequest? body, CancellationToken ct)
    {
        if (body?.Id is { } bodyId && bodyId != id) return BadRequest();
        var result = await Mediator.Send(new ApproveTimeEntryCommand(id), ct);
        return Ok(result);
    }

    public sealed record UpdateTimeEntryRequest(DateOnly ActivityDate, decimal Hours, decimal HourlyRate, string TaskDescription, bool IsBillable, Guid? ConsultantUserId = null, string? ConsultantName = null, string? CustomFields = null);

    public sealed record ApproveTimeEntryRequest(Guid? Id = null, Guid? ApprovedByUserId = null);
}
