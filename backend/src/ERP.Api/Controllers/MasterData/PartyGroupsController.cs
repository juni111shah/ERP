using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.MasterData;

/// <summary>Party groups (customer and vendor categories).</summary>
[Route("api/v1/party-groups")]
public sealed class PartyGroupsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.PartyGroupsRead)]
    public Task<PagedResult<PartyGroupDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PartyGroupDto.Projection, CorePermissions.PartyGroupsRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.PartyGroupsWrite)]
    public async Task<IActionResult> Create(PartyGroupInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePartyGroupCommand(input), ct);
        return Created($"/api/v1/party-groups/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.PartyGroupsWrite)]
    public Task<PartyGroupDto> Update(Guid id, PartyGroupInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePartyGroupCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.PartyGroupsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePartyGroupCommand(id), ct);
        return NoContent();
    }
}
