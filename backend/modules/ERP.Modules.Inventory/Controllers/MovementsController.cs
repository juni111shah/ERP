using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Inventory.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Inventory.Controllers;

/// <summary>The stock ledger.</summary>
[ModuleGate(InventoryModule.Key)]
[Route("api/v1/m/inventory/movements")]
public sealed class MovementsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<PagedResult<MovementDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, MovementDto.Projection, InventoryModule.StockRead, ct);
}
