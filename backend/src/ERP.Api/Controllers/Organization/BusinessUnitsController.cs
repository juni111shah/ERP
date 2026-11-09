using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Organization;

/// <summary>Business units (branches, stores, sites) of the legal entities.</summary>
[Route("api/v1/business-units")]
public sealed class BusinessUnitsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.BusinessUnitsRead)]
    public Task<PagedResult<BusinessUnitDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<BusinessUnit, BusinessUnitDto>(query, "businessunit", BusinessUnitDto.From, CorePermissions.BusinessUnitsRead, ct);

    [HttpPost]
    [RequirePermission(CorePermissions.BusinessUnitsWrite)]
    public async Task<IActionResult> Create(BusinessUnitInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateBusinessUnitCommand(input), ct);
        return Created($"/api/v1/business-units/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.BusinessUnitsWrite)]
    public Task<BusinessUnitDto> Update(Guid id, BusinessUnitInput input, CancellationToken ct) => Mediator.Send(new UpdateBusinessUnitCommand(id, input), ct);
}
