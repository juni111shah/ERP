using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Project tasks and their dependencies.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/tasks")]
public sealed class TasksController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary><c>?live=true</c> leaves out the tasks of templates and archived projects.</summary>
    [HttpGet]
    [RequirePermission(P.TasksRead)]
    public Task<PagedResult<TaskDto>> List([FromQuery] PagedQuery query, bool? live, CancellationToken ct)
        => Mediator.Send(ProjectLists.Tasks(query, live == true), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.TasksRead)]
    public Task<TaskDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetTaskQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.TasksManage)]
    public async Task<IActionResult> Create(TaskInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateTaskCommand(input), ct);
        return Created($"/api/v1/m/projects/tasks/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.TasksManage)]
    public Task<TaskDto> Update(Guid id, TaskInput input, CancellationToken ct)
        => Mediator.Send(new UpdateTaskCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.TasksManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteTaskCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.TasksManage)]
    public Task<TaskDto> ChangeStatus(Guid id, ChangeTaskStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeTaskStatusCommand(id, body.Status), ct);

    [HttpPut("{id:guid}/dependencies")]
    [RequirePermission(P.TasksManage)]
    public Task<TaskDto> SetDependencies(Guid id, DependenciesRequest body, CancellationToken ct)
        => Mediator.Send(new SetTaskDependenciesCommand(id, body.DependsOn), ct);

    public sealed record ChangeTaskStatusRequest(ProjectTaskStatus Status);

    /// <summary>The tasks that must finish before this one (replaces the list).</summary>
    public sealed record DependenciesRequest(IReadOnlyList<Guid> DependsOn);
}
