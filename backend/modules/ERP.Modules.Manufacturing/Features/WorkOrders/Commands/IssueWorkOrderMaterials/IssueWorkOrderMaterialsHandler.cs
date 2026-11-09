using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class IssueWorkOrderMaterialsHandler(
    IWorkOrderRepository orders,
    IBillOfMaterialsRepository boms,
    IItemRepository items,
    WorkOrderMaterials materials,
    IUnitOfWork unitOfWork)
    : ICommandHandler<IssueWorkOrderMaterialsCommand, WorkOrderDto>
{
    public Task<WorkOrderDto> Handle(IssueWorkOrderMaterialsCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Only an order in progress takes material.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.WorkOrderId, token);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Release the work order before issuing material to it.");
        var date = WorkOrderRules.DateOrToday(request.Date, "date");

        // 2. What to issue: the lines sent, or what the BOM still needs.
        List<MaterialNeed> needs;
        if (request.Lines is { Count: > 0 })
        {
            // Actual consumption can differ from the BOM, so any stocked product can be issued, in any amount.
            var ids = request.Lines.Select(l => l.ItemId).Distinct().ToList();
            var products = await items.GetProductsAsync(ids, token);
            var missing = ids.FirstOrDefault(id => !products.ContainsKey(id));
            if (missing != Guid.Empty) throw new FieldValidationException("lines", $"Item {missing} was not found or is not a product.");
            needs = request.Lines.Select(l => new MaterialNeed(l.ItemId, products[l.ItemId].Sku, products[l.ItemId].Name, l.Quantity, l.Notes)).ToList();
        }
        else
        {
            var bom = await boms.GetWithComponentsAsync(wo.BomId, token);
            needs = await materials.BomNeedsAsync(wo, bom, wo.PlannedQuantity, token);
            if (needs.Count == 0) throw new ConflictException("The bill of materials for this order is already fully issued.");
        }

        // 3. Issue and save.
        await materials.IssueAsync(wo, needs, date, backflushed: false, request.Notes, token);
        await orders.SaveChangesAsync(token);
        return WorkOrderDto.From(wo);
    }, cancellationToken);
}
