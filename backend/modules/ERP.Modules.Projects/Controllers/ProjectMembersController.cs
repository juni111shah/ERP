using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>The team of a project.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/projects")]
public sealed class ProjectMembersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("{id:guid}/members")]
    [RequirePermission(P.ProjectsRead)]
    public Task<IReadOnlyList<MemberDto>> List(Guid id, CancellationToken ct)
        => Mediator.Send(new ListMembersQuery(id), ct);

    [HttpPost("{id:guid}/members")]
    [RequirePermission(P.TeamManage)]
    public async Task<IActionResult> Add(Guid id, MemberRequest body, CancellationToken ct)
        => Created($"/api/v1/m/projects/projects/{id}/members", await Mediator.Send(new AddMemberCommand(id, body.UserId, body.Role), ct));

    [HttpDelete("members/{memberId:guid}")]
    [RequirePermission(P.TeamManage)]
    public async Task<IActionResult> Remove(Guid memberId, CancellationToken ct)
    {
        await Mediator.Send(new RemoveMemberCommand(memberId), ct);
        return NoContent();
    }

    public sealed record MemberRequest(Guid UserId, string? Role);
}
