using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Tenant portal: the signed-in customer's own leases, schedule and maintenance requests.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/portal")]
public sealed class TenantPortalController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("leases")]
    [Authorize(Policy = "portal.customer")]
    public Task<IReadOnlyList<PortalLeaseDto>> GetLeases(CancellationToken ct)
        => Mediator.Send(new GetPortalLeasesQuery(), ct);

    [HttpGet("requests")]
    [Authorize(Policy = "portal.customer")]
    public Task<IReadOnlyList<PortalRequestDto>> ListRequests(CancellationToken ct)
        => Mediator.Send(new ListPortalRequestsQuery(), ct);

    [HttpPost("requests")]
    [Authorize(Policy = "portal.customer")]
    public async Task<IActionResult> CreateRequest(PortalRequestInput input, CancellationToken ct)
        => Created("/api/v1/m/realestate/portal/requests", await Mediator.Send(new CreatePortalRequestCommand(input), ct));
}
