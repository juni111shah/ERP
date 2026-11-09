using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Inventory.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Inventory.Controllers;

/// <summary>Physical stock counts.</summary>
[ModuleGate(InventoryModule.Key)]
[Route("api/v1/m/inventory/counts")]
public sealed class StockCountsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(InventoryModule.CountsRead)]
    public Task<PagedResult<StockCountRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, StockCountRow.Projection, InventoryModule.CountsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(InventoryModule.CountsRead)]
    public Task<StockCountDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetStockCountQuery(id), ct);

    [HttpPost]
    [RequirePermission(InventoryModule.CountsManage)]
    public async Task<IActionResult> Create(StockCountInput input, CancellationToken ct)
    {
        var c = await Mediator.Send(new CreateStockCountCommand(input), ct);
        return Created($"/api/v1/m/inventory/counts/{c.Id}", c);
    }

    [HttpPut("{id:guid}/lines")]
    [RequirePermission(InventoryModule.CountsManage)]
    public Task<StockCountDto> SaveLines(Guid id, CountedLinesRequest body, CancellationToken ct)
        => Mediator.Send(new SaveCountedQuantitiesCommand(id, body.Lines), ct);

    [HttpPost("{id:guid}/post")]
    [RequirePermission(InventoryModule.CountsManage)]
    public Task<StockCountDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostStockCountCommand(id), ct);

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(InventoryModule.CountsManage)]
    public Task<StockCountDto> Cancel(Guid id, CancellationToken ct)
        => Mediator.Send(new CancelStockCountCommand(id), ct);

    /// <summary>The counted quantities to enter.</summary>
    public sealed record CountedLinesRequest(List<CountedLineInput> Lines);
}
