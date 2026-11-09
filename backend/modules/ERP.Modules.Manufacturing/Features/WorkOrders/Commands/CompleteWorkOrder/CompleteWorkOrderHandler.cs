using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class CompleteWorkOrderHandler(
    IWorkOrderRepository orders,
    IBillOfMaterialsRepository boms,
    IItemRepository items,
    WorkOrderMaterials materials,
    IStockPostingService stockPosting,
    IPostingService posting,
    ISettingsService settings,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CompleteWorkOrderCommand, WorkOrderDto>
{
    public Task<WorkOrderDto> Handle(CompleteWorkOrderCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Load it; made and scrapped together stay within the planned quantity plus the allowed over-completion.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Completed, isSystem: false);

        var made = request.CompletedQuantity;
        var scrapped = request.ScrappedQuantity;
        var overPercent = await settings.GetAsync<decimal?>("manufacturing.overCompletionPercent", wo.LegalEntityId, token) ?? 0m;
        var allowed = Math.Round(wo.PlannedQuantity * (1 + overPercent / 100m), 4);
        if (made + scrapped > allowed)
            throw new FieldValidationException("completedQuantity", $"This order is for {wo.PlannedQuantity:0.####}; made and scrapped together cannot pass {allowed:0.####}.");

        // 2. With stock tracked, only a stocked product can be received.
        var date = WorkOrderRules.DateOrToday(request.CompletionDate, "completionDate");
        var stockOn = await stockPosting.IsInventoryEnabledAsync(token);
        var bom = await boms.GetWithComponentsAsync(wo.BomId, token);
        var backflush = await settings.GetAsync<bool?>("manufacturing.autoConsumeComponents", wo.LegalEntityId, token) ?? true;

        var producedType = await items.GetTypeAsync(wo.ProducedItemId, token);
        if (stockOn && made > 0 && producedType != ItemType.Product)
            throw new ConflictException($"'{wo.ProducedItemCode}' is not a stocked product, so the finished goods cannot be received.");

        // 3. Record the hours, complete the operations and backflush what the BOM still needs.
        foreach (var hours in request.OperationHours ?? [])
        {
            var op = wo.Operations.FirstOrDefault(o => o.Id == hours.OperationId) ?? throw new FieldValidationException("operationHours", "That operation is not on this work order.");
            op.ActualHours = hours.ActualHours;
        }
        foreach (var op in wo.Operations) op.Status = OperationStatus.Completed;

        if (backflush)
        {
            var needs = await materials.BomNeedsAsync(wo, bom, made + scrapped, token);
            await materials.IssueAsync(wo, needs, date, backflushed: true, null, token);
        }

        wo.CompletionDate = date;
        wo.Status = WorkOrderStatus.Completed;
        wo.CompletedQuantity = made;
        wo.ScrappedQuantity = scrapped;
        if (!string.IsNullOrWhiteSpace(request.Notes)) wo.Notes = (wo.Notes + " | Completed: " + request.Notes).Trim();

        // 4. Cost it. Conversion is what the operations took (actual hours where recorded), else the BOM's labour and overhead for every unit worked on.
        var opCost = Math.Round(wo.Operations.Sum(o => (o.ActualHours > 0 ? o.ActualHours : o.PlannedHours) * o.HourlyRate), 2);
        var conversion = opCost > 0 ? opCost : Math.Round((bom.LaborCostPerUnit + bom.OverheadCostPerUnit) * (made + scrapped), 2);
        var material = WorkOrderRules.NetIssued(wo).Values.Sum(v => v.Value);
        var total = material + conversion;
        var goodValue = Math.Round(total * made / (made + scrapped), 2);
        var scrapValue = total - goodValue;

        wo.MaterialCost = material;
        wo.ConversionCost = conversion;
        wo.ScrapCost = scrapValue;
        wo.ActualTotalCost = total;

        // 5. Receive the goods and post. With Inventory off nothing was received or issued, so there is no stock for the ledger to carry: the cost is recorded on the order only.
        if (stockOn)
        {
            if (made > 0)
            {
                await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                    wo.LegalEntityId, wo.ProducedItemId, made, Math.Round(goodValue / made, 4), date, wo.WorkOrderNumber,
                    $"Produced via WO {wo.WorkOrderNumber} ({wo.ProducedItemCode})", wo.WarehouseId, SkipJournal: true), token);
            }

            if (total > 0)
            {
                var wip = await posting.AccountForRoleAsync(SystemRoles.Wip, token);
                var lines = new List<PostingLine>();
                if (conversion > 0)
                {
                    lines.Add(new(wip, conversion, 0, $"Conversion cost into WIP - WO {wo.WorkOrderNumber}"));
                    lines.Add(new(await RoleOrFallbackAsync(WorkOrderRules.AbsorptionRole, SystemRoles.PurchasesExpense, token), 0, conversion, $"Conversion cost absorbed - WO {wo.WorkOrderNumber}"));
                }
                if (goodValue > 0) lines.Add(new(await posting.AccountForRoleAsync("inventory", token), goodValue, 0, $"Finished goods produced - WO {wo.WorkOrderNumber}"));
                if (scrapValue > 0) lines.Add(new(await RoleOrFallbackAsync(WorkOrderRules.ScrapRole, "cogs", token), scrapValue, 0, $"Scrapped units - WO {wo.WorkOrderNumber}"));
                lines.Add(new(wip, 0, total, $"WIP released - WO {wo.WorkOrderNumber}"));

                await posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, WorkOrderRules.JournalSource, wo.Id, $"Production completion WO {wo.WorkOrderNumber}", wo.WorkOrderNumber, lines), token);
            }
        }

        // 6. Save.
        await orders.SaveChangesAsync(token);
        return WorkOrderDto.From(wo);
    }, cancellationToken);

    private async Task<Guid> RoleOrFallbackAsync(string role, string fallback, CancellationToken ct)
    {
        try { return await posting.AccountForRoleAsync(role, ct); }
        catch (ConflictException) { return await posting.AccountForRoleAsync(fallback, ct); }
    }
}
