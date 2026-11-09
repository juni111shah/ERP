using ERP.Api.Infrastructure;
using ERP.Application.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The people who can open the platform console.</summary>
[Route("api/v1/platform-admin/team")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class PlatformTeamController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public Task<IReadOnlyList<PlatformTeamMember>> List(CancellationToken ct) => Mediator.Send(new ListPlatformTeamQuery(), ct);

    [HttpPost]
    public Task<NewTeamMember> Add(TeamMemberInput input, CancellationToken ct) => Mediator.Send(new AddPlatformTeamMemberCommand(input), ct);

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken ct)
    {
        await Mediator.Send(new RemovePlatformTeamMemberCommand(userId), ct);
        return NoContent();
    }
}
