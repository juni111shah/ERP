using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>A project's schedule (critical path) and its baseline.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/projects")]
public sealed class ProjectSchedulesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("{id:guid}/schedule")]
    [RequirePermission(P.TasksRead)]
    public Task<ScheduleDto> Get(Guid id, CancellationToken ct)
        => Mediator.Send(new GetProjectScheduleQuery(id), ct);

    [HttpPost("{id:guid}/baseline")]
    [RequirePermission(P.TasksManage)]
    public Task<ScheduleDto> SetBaseline(Guid id, BaselineRequest body, CancellationToken ct)
        => Mediator.Send(new SetProjectBaselineCommand(id, body.Clear), ct);

    /// <summary>True removes the baseline instead of saving the current plan.</summary>
    public sealed record BaselineRequest(bool Clear);
}
