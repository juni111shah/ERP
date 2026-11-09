using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Distribution.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Distribution.Controllers;

/// <summary>Wholesale price tiers.</summary>
[ModuleGate(DistributionModule.Key)]
[Route("api/v1/m/distribution/wholesale")]
public sealed class WholesaleController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.PricingRead)]
    public Task<PagedResult<WholesalePriceMatrixDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, WholesalePriceMatrixDto.Projection, Permissions.PricingRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PricingRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var price = await Mediator.Send(new GetWholesalePriceByIdQuery(id), ct);
        return price is null ? NotFound() : Ok(price);
    }

    [HttpPost]
    [RequirePermission(Permissions.PricingManage)]
    public async Task<IActionResult> Create(CreateWholesalePriceCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/distribution/wholesale/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PricingManage)]
    public Task<WholesalePriceMatrixDto> Update(Guid id, UpdateWholesalePriceCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PricingManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteWholesalePriceCommand(id), ct);
        return NoContent();
    }

    [HttpPost("calculate-rate")]
    [RequirePermission(Permissions.PricingRead)]
    public Task<WholesaleRateCalculationDto> CalculateRate(CalculateWholesaleRateQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);
}
