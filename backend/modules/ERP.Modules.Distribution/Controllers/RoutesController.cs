using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Distribution.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Distribution.Controllers;

/// <summary>Delivery routes and their stops.</summary>
[ModuleGate(DistributionModule.Key)]
[Route("api/v1/m/distribution/routes")]
public sealed class RoutesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.RoutesRead)]
    public Task<PagedResult<DistributionRouteSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, DistributionRouteSummaryDto.Projection, Permissions.RoutesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RoutesRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var route = await Mediator.Send(new GetRouteByIdQuery(id), ct);
        return route is null ? NotFound() : Ok(route);
    }

    [HttpPost]
    [RequirePermission(Permissions.RoutesManage)]
    public async Task<IActionResult> Create(CreateRouteCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/distribution/routes/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RoutesManage)]
    public Task<DistributionRouteDto> Update(Guid id, UpdateRouteCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RoutesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRouteCommand(id), ct);
        return NoContent();
    }
}
