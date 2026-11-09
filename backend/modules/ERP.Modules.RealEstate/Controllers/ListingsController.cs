using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Listings of properties and units for sale or to let.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/listings")]
public sealed class ListingsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.CrmRead)]
    public Task<PagedResult<ListingDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, ListingDto.Projection, P.CrmRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.CrmRead)]
    public Task<ListingDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetListingQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.CrmManage)]
    public async Task<IActionResult> Create(ListingInput i, CancellationToken ct)
    {
        var c = await Mediator.Send(new CreateListingCommand(i), ct);
        return Created($"/api/v1/m/realestate/listings/{c.Id}", c);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.CrmManage)]
    public Task<ListingDto> Update(Guid id, ListingInput i, CancellationToken ct)
        => Mediator.Send(new UpdateListingCommand(id, i), ct);

    [HttpPost("{id:guid}/status")]
    [RequirePermission(P.CrmManage)]
    public Task<ListingDto> ChangeStatus(Guid id, ListingStatusRequest b, CancellationToken ct)
        => Mediator.Send(new ChangeListingStatusCommand(id, b.Status), ct);

    public sealed record ListingStatusRequest(ListingStatus Status);
}
