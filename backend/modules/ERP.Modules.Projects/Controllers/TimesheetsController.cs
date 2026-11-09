using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Time logged on projects: single entries, the weekly grid, approval and write-off.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/timesheets")]
public sealed class TimesheetsController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary><c>?live=true</c> leaves out the time of templates and archived projects.</summary>
    [HttpGet]
    [RequirePermission(P.TimesheetsRead)]
    public Task<PagedResult<TimesheetDto>> List([FromQuery] PagedQuery query, bool? live, CancellationToken ct)
        => Mediator.Send(ProjectLists.Timesheets(query, live == true), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.TimesheetsRead)]
    public Task<TimesheetDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetTimesheetQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.TimesheetsManage)]
    public async Task<IActionResult> Create(TimesheetInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateTimesheetCommand(input), ct);
        return Created($"/api/v1/m/projects/timesheets/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.TimesheetsManage)]
    public Task<TimesheetDto> Update(Guid id, TimesheetInput input, CancellationToken ct)
        => Mediator.Send(new UpdateTimesheetCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.TimesheetsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteTimesheetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("week")]
    [RequirePermission(P.TimesheetsManage)]
    public Task<IReadOnlyList<TimesheetDto>> SaveWeek(SaveTimesheetWeekRequest body, CancellationToken ct)
        => Mediator.Send(new SaveTimesheetWeekCommand(body.Items), ct);

    [HttpPost("submit")]
    [RequirePermission(P.TimesheetsManage)]
    public async Task<IActionResult> Submit(IdsRequest body, CancellationToken ct)
        => Ok(new { submitted = await Mediator.Send(new SubmitTimesheetsCommand(body.Ids), ct) });

    [HttpPost("decide")]
    [RequirePermission(P.TimesheetsApprove)]
    public async Task<IActionResult> Decide(DecideRequest body, CancellationToken ct)
        => Ok(new { decided = await Mediator.Send(new DecideTimesheetsCommand(body.Ids, body.Approve, body.Reason), ct) });

    [HttpPost("write-off")]
    [RequirePermission(P.BillingManage)]
    public async Task<IActionResult> WriteOff(IdsRequest body, CancellationToken ct)
        => Ok(new { writtenOff = await Mediator.Send(new WriteOffTimesheetsCommand(body.Ids), ct) });

    /// <summary>The rows of the weekly grid.</summary>
    public sealed record SaveTimesheetWeekRequest(IReadOnlyList<TimesheetInput> Items);
}
