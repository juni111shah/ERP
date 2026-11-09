using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Projects: the list, the detail, its status, cloning, archiving and the activity timeline.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/projects")]
public sealed class ProjectsController(ISender mediator) : ApiControllerBase(mediator)
{
    /// <summary><c>?live=true</c> leaves out templates and archived projects.</summary>
    [HttpGet]
    [RequirePermission(P.ProjectsRead)]
    public Task<PagedResult<ProjectSummaryDto>> List([FromQuery] PagedQuery query, bool? live, CancellationToken ct)
        => Mediator.Send(ProjectLists.Projects(query, live == true), ct);

    [HttpGet("kpis")]
    [RequirePermission(P.ProjectsRead)]
    public Task<ProjectKpiDto> Kpis(CancellationToken ct)
        => Mediator.Send(new GetProjectKpiSummaryQuery(), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ProjectsRead)]
    public Task<ProjectDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetProjectQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ProjectsManage)]
    public async Task<IActionResult> Create(ProjectInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateProjectCommand(input), ct);
        return Created($"/api/v1/m/projects/projects/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.ProjectsManage)]
    public Task<ProjectDto> Update(Guid id, ProjectInput input, CancellationToken ct)
        => Mediator.Send(new UpdateProjectCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ProjectsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteProjectCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.ProjectsManage)]
    public Task<ProjectDto> ChangeStatus(Guid id, ChangeProjectStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeProjectStatusCommand(id, body.Status), ct);

    [HttpPost("{id:guid}/clone")]
    [RequirePermission(P.ProjectsManage)]
    public async Task<IActionResult> Clone(Guid id, CloneInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CloneProjectCommand(id, input), ct);
        return Created($"/api/v1/m/projects/projects/{created.Id}", created);
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission(P.ProjectsManage)]
    public Task<ProjectDto> Archive(Guid id, ArchiveRequest body, CancellationToken ct)
        => Mediator.Send(new ArchiveProjectCommand(id, body.Archive), ct);

    [HttpGet("{id:guid}/activity")]
    [RequirePermission(P.ProjectsRead)]
    public Task<IReadOnlyList<ActivityDto>> Activity(Guid id, int? take, CancellationToken ct)
        => Mediator.Send(new GetProjectActivityQuery(id, take ?? 100), ct);

    public sealed record ChangeProjectStatusRequest(ProjectStatus Status);

    /// <summary>True archives the project, false restores it.</summary>
    public sealed record ArchiveRequest(bool Archive);
}
