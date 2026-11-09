using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Vendor portal: the work orders assigned to the signed-in vendor.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/portal/vendor/work-orders")]
public sealed class VendorPortalController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [Authorize(Policy = "portal.vendor")]
    public Task<IReadOnlyList<VendorWorkOrderDto>> List(CancellationToken ct)
        => Mediator.Send(new ListVendorWorkOrdersQuery(), ct);

    [HttpPost("{id:guid}/update")]
    [Authorize(Policy = "portal.vendor")]
    public Task<VendorWorkOrderDto> Update(Guid id, VendorWorkOrderUpdate body, CancellationToken ct)
        => Mediator.Send(new UpdateVendorWorkOrderCommand(id, body), ct);
}
