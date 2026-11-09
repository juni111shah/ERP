using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Manufacturing.Controllers;

/// <summary>Product costing and variance.</summary>
[ModuleGate(ManufacturingModule.Key)]
[Route("api/v1/m/manufacturing/costing")]
public sealed class CostingController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("rollup/{bomId:guid}")]
    [RequirePermission(Permissions.CostingRead)]
    public Task<ProductCostRollupDto?> Rollup(Guid bomId, Guid? warehouseId, CancellationToken ct)
        => Mediator.Send(new CalculateProductCostingRollupQuery(bomId, warehouseId), ct);

    [HttpGet("variance")]
    [RequirePermission(Permissions.CostingRead)]
    public Task<ProductCostVarianceDto?> Variance(Guid legalEntityId, Guid itemId, CancellationToken ct)
        => Mediator.Send(new GetProductCostVarianceQuery(legalEntityId, itemId), ct);
}
