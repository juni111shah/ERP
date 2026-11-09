using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Inventory.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Inventory.Controllers;

/// <summary>Stock reports: valuation, movement summary, slow-moving goods, reorder suggestions and the stock/ledger reconciliation.</summary>
[ModuleGate(InventoryModule.Key)]
[Route("api/v1/m/inventory/reports")]
public sealed class InventoryReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("valuation")]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<ValuationDto> Valuation(DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetStockValuationQuery(asOf ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow)), ct);

    [HttpGet("movement-summary")]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<MovementSummaryDto> MovementSummary(DateOnly from, DateOnly to, CancellationToken ct)
        => Mediator.Send(new GetMovementSummaryQuery(from, to), ct);

    [HttpGet("slow-moving")]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<SlowMovingDto> SlowMoving(DateOnly? asOf, int? days, CancellationToken ct)
        => Mediator.Send(new GetSlowMovingQuery(asOf ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), days ?? 90), ct);

    [HttpGet("reorder")]
    [RequirePermission(InventoryModule.StockRead)]
    public Task<ReorderDto> Reorder(CancellationToken ct)
        => Mediator.Send(new GetReorderSuggestionsQuery(), ct);

    [HttpGet("stock-gl-reconciliation")]
    [RequirePermission(InventoryModule.ReconciliationRead)]
    public Task<StockGlReconciliationDto> StockGlReconciliation(DateOnly? asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetStockGlReconciliationQuery(asOf ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), legalEntityId), ct);
}
