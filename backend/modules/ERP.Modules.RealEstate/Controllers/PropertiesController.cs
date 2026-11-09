using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Properties (buildings, villas, plots) with their custom fields.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/properties")]
public sealed class PropertiesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.PropertiesRead)]
    public Task<PagedResult<PropertyDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<Property, PropertyDto>(query, Docs.Property, PropertyDto.From, P.PropertiesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.PropertiesRead)]
    public Task<PropertySummaryDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPropertyQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.PropertiesManage)]
    public async Task<IActionResult> Create(PropertyInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePropertyCommand(input), ct);
        return Created($"/api/v1/m/realestate/properties/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.PropertiesManage)]
    public Task<PropertyDto> Update(Guid id, PropertyInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePropertyCommand(id, input), ct);
}
