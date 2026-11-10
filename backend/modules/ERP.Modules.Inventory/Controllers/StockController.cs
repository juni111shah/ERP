using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Inventory.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Inventory.Controllers;

/// <summary>Stock on hand, receipts, issues, adjustments, transfers and reorder levels.</summary>
[ModuleGate(InventoryModule.Key)]
[Route("api/v1/m/inventory/stock")]
public sealed class StockController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<StockOnHandDto> OnHand(Guid? warehouseId, Guid? itemId, CancellationToken ct)
        => Mediator.Send(new GetStockOnHandQuery(warehouseId, itemId), ct);

    [HttpGet("low")]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<IReadOnlyList<LowStockRow>> Low(CancellationToken ct)
        => Mediator.Send(new GetLowStockQuery(), ct);

    [HttpPost("receive")]
    [RequirePermission(InventoryModule.StockWrite)]
    public async Task<IActionResult> Receive(ReceiveStockInput input, CancellationToken ct)
        => Created("/api/v1/m/inventory/movements", await Mediator.Send(new ReceiveStockCommand(input), ct));

    [HttpPost("issue")]
    [RequirePermission(InventoryModule.StockWrite)]
    public async Task<IActionResult> Issue(IssueStockInput input, CancellationToken ct)
        => Created("/api/v1/m/inventory/movements", await Mediator.Send(new IssueStockCommand(input), ct));

    [HttpPost("adjust")]
    [RequirePermission(InventoryModule.StockWrite)]
    public async Task<IActionResult> Adjust(AdjustStockInput input, CancellationToken ct)
        => Created("/api/v1/m/inventory/movements", await Mediator.Send(new AdjustStockCommand(input), ct));

    [HttpPost("transfer")]
    [RequirePermission(InventoryModule.StockWrite)]
    public async Task<IActionResult> Transfer(TransferStockInput input, CancellationToken ct)
        => Created("/api/v1/m/inventory/movements", await Mediator.Send(new TransferStockCommand(input), ct));

    [HttpPut("policy")]
    [RequirePermission(InventoryModule.StockWrite)]
    public async Task<IActionResult> SetPolicy(StockPolicyInput input, CancellationToken ct)
    {
        await Mediator.Send(new SetStockPolicyCommand(input), ct);
        return NoContent();
    }
}
