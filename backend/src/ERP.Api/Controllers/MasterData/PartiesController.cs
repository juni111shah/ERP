using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.MasterData;

/// <summary>Parties: customers, vendors, employees and others.</summary>
[Route("api/v1/parties")]
public sealed class PartiesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.PartiesRead)]
    public Task<PagedResult<PartyDto>> List([FromQuery] ListPartiesQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.PartiesRead)]
    public Task<PartyDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPartyByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.PartiesWrite)]
    public async Task<IActionResult> Create(PartyInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePartyCommand(input), ct);
        return Created($"/api/v1/parties/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.PartiesWrite)]
    public Task<PartyDto> Update(Guid id, PartyInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePartyCommand(id, input), ct);
}
