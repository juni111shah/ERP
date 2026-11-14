using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>How this workspace runs its projects (for every signed-in screen).</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/config")]
public sealed class ProjectsConfigController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [SignedIn("The workspace's timesheet rules, needed by every time-entry screen")]
    public Task<ProjectsConfigDto> Get(CancellationToken ct)
        => Mediator.Send(new GetProjectsConfigQuery(), ct);
}
