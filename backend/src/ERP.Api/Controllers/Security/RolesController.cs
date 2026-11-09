using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Security;

/// <summary>The workspace's roles and their permissions.</summary>
[Route("api/v1/roles")]
public sealed class RolesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.RolesRead)]
    public Task<PagedResult<RoleDto>> List([FromQuery] PagedQuery query, CancellationToken ct) => PagedList(query, RoleDto.Projection, CorePermissions.RolesRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.RolesWrite)]
    public async Task<IActionResult> Create(RoleInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateRoleCommand(input), ct);
        return Created($"/api/v1/roles/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.RolesWrite)]
    public Task<CreatedRoleDto> Update(Guid id, RoleInput input, CancellationToken ct) => Mediator.Send(new UpdateRoleCommand(id, input), ct);
}
