using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Distribution.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Distribution.Controllers;

/// <summary>Van sales trips.</summary>
[ModuleGate(DistributionModule.Key)]
[Route("api/v1/m/distribution/trips")]
public sealed class TripsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.TripsRead)]
    public Task<PagedResult<VanSalesTripSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, VanSalesTripSummaryDto.Projection, Permissions.TripsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.TripsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var trip = await Mediator.Send(new GetTripByIdQuery(id), ct);
        return trip is null ? NotFound() : Ok(trip);
    }

    [HttpPost]
    [RequirePermission(Permissions.TripsManage)]
    public async Task<IActionResult> Create(CreateTripCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/distribution/trips/{created.Id}", created);
    }

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(Permissions.TripsManage)]
    public Task<VanSalesTripDto> Complete(Guid id, CompleteTripCommand command, CancellationToken ct)
        => Mediator.Send(command with { TripId = id }, ct);
}
