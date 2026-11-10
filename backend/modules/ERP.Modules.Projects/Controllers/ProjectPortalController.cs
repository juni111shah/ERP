using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>The customer portal: the customer's projects and the sign-offs asked of them.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/portal")]
[Authorize(Policy = "portal.customer")]
public sealed class ProjectPortalController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("projects")]
    public Task<IReadOnlyList<PortalProjectDto>> Projects(CancellationToken ct)
        => Mediator.Send(new GetPortalProjectsQuery(), ct);

    [HttpGet("acceptances")]
    public Task<IReadOnlyList<PortalAcceptanceDto>> Acceptances(CancellationToken ct)
        => Mediator.Send(new ListPortalAcceptancesQuery(), ct);

    /// <summary>The caller's IP is kept on the sign-off as evidence.</summary>
    [HttpPost("acceptances/{id:guid}/sign")]
    public Task<PortalAcceptanceDto> Sign(Guid id, SignAcceptanceInput body, CancellationToken ct)
        => Mediator.Send(new SignAcceptanceCommand(id, body, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
}
