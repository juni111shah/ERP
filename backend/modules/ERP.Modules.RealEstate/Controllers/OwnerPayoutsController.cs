using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Payouts to the owners of managed properties.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/owner-payouts")]
public sealed class OwnerPayoutsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.OwnerPayoutsRead)]
    public Task<PagedResult<OwnerPayoutDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, OwnerPayoutDto.Projection, P.OwnerPayoutsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.OwnerPayoutsRead)]
    public Task<OwnerPayoutDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetOwnerPayoutQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.OwnerPayoutsManage)]
    public async Task<IActionResult> Create(OwnerPayoutInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateOwnerPayoutCommand(input), ct);
        return Created($"/api/v1/m/realestate/owner-payouts/{created.Id}", created);
    }

    [HttpPost("{id:guid}/void")]
    [RequirePermission(P.OwnerPayoutsManage)]
    public Task<OwnerPayoutDto> Void(Guid id, VoidOwnerPayoutRequest body, CancellationToken ct)
        => Mediator.Send(new VoidOwnerPayoutCommand(id, body.Reason), ct);

    public sealed record VoidOwnerPayoutRequest(string? Reason);
}
