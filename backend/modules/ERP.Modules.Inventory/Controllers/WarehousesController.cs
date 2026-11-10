using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Inventory.Controllers;

/// <summary>Warehouses.</summary>
[ModuleGate(InventoryModule.Key)]
[Route("api/v1/m/inventory/warehouses")]
public sealed class WarehousesController(ISender mediator) : ApiControllerBase(mediator)
{
    // One line: paged list with search / filter / sort from the [Listable]/[Filterable] attributes.
    [HttpGet]
    [RequirePermission(InventoryModule.WarehousesRead)]
    public Task<PagedResult<object>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<Warehouse, object>(query, "inventory.warehouse", WarehouseRow.From, InventoryModule.WarehousesRead, ct);

    [HttpPost]
    [RequirePermission(InventoryModule.WarehousesWrite)]
    public async Task<IActionResult> Create(WarehouseInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateWarehouseCommand(input), ct);
        return Created($"/api/v1/m/inventory/warehouses/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(InventoryModule.WarehousesWrite)]
    public Task<WarehouseDto> Update(Guid id, WarehouseUpdateInput input, CancellationToken ct)
        => Mediator.Send(new UpdateWarehouseCommand(id, input), ct);
}
