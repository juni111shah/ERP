using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Unit types (studio, 1BR, office...).</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/unit-types")]
public sealed class UnitTypesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.UnitTypesRead)]
    public Task<PagedResult<UnitTypeDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, UnitTypeDto.Projection, P.UnitTypesRead, ct);

    [HttpPost]
    [RequirePermission(P.UnitTypesManage)]
    public async Task<IActionResult> Create(UnitTypeInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateUnitTypeCommand(input), ct);
        return Created($"/api/v1/m/realestate/unit-types/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.UnitTypesManage)]
    public Task<UnitTypeDto> Update(Guid id, UnitTypeInput input, CancellationToken ct)
        => Mediator.Send(new UpdateUnitTypeCommand(id, input), ct);
}
