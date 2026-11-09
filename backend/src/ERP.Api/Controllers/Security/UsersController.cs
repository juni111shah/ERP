using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Security;

/// <summary>The members of the workspace: invitations, roles and activation.</summary>
[Route("api/v1/users")]
public sealed class UsersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.UsersRead)]
    public Task<IReadOnlyList<UserSummary>> List(CancellationToken ct) => Mediator.Send(new ListUsersQuery(), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.UsersWrite)]
    public async Task<IActionResult> Invite(InviteUserInput input, CancellationToken ct)
    {
        var invited = await Mediator.Send(new InviteUserCommand(input), ct);
        return Created($"/api/v1/users/{invited.Id}", invited);
    }

    [HttpPut("{id:guid}/roles")]
    [RequirePermission(CorePermissions.UsersWrite)]
    public Task<UserSummary> SetRoles(Guid id, UserRolesRequest body, CancellationToken ct) => Mediator.Send(new SetUserRolesCommand(id, body.RoleIds), ct);

    [HttpPost("{id:guid}/active")]
    [RequirePermission(CorePermissions.UsersWrite)]
    public Task<UserSummary> SetActive(Guid id, UserActiveRequest body, CancellationToken ct) => Mediator.Send(new SetUserActiveCommand(id, body.IsActive), ct);

    public sealed record UserRolesRequest(Guid[] RoleIds);
    public sealed record UserActiveRequest(bool IsActive);
}
