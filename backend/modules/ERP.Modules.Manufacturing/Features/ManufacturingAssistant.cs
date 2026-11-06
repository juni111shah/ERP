using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Manufacturing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class WorkOrdersAssistantReader(IReadRepository<WorkOrder> orders, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "production_work_orders";
    public string Description => "Production work orders, newest first: number, product, planned and produced quantity, dates, status and cost.";
    public string? ModuleKey => ManufacturingModule.Key;
    public string Permission => Permissions.WorkOrdersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = orders.Query();
        if (search is not null) query = query.Where(w => w.WorkOrderNumber.Contains(search) || w.ProducedItemCode.Contains(search) || w.ProducedItemName.Contains(search) || w.BomCode.Contains(search));
        return await query.OrderByDescending(w => w.StartDate).Take(take).Select(w => new { Number = w.WorkOrderNumber, Bom = w.BomCode, Product = w.ProducedItemName, ProductCode = w.ProducedItemCode, w.PlannedQuantity, w.CompletedQuantity, w.Uom, w.StartDate, w.DueDate, Status = w.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == w.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), w.EstimatedTotalCost, w.ActualTotalCost, Link = "/manufacturing/workorders" }).ToListAsync(ct);
    }
}

internal sealed class BomsAssistantReader(IReadRepository<BillOfMaterials> boms) : IAssistantReader
{
    public string Key => "bills_of_materials";
    public string Description => "Bills of materials: code, name, product, batch size, version, number of components and whether active.";
    public string? ModuleKey => ManufacturingModule.Key;
    public string Permission => Permissions.BomsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = boms.Query();
        if (search is not null) query = query.Where(b => b.Code.Contains(search) || b.Name.Contains(search) || b.ProducedItemCode.Contains(search) || b.ProducedItemName.Contains(search));
        return await query.OrderBy(b => b.Code).Take(take).Select(b => new { b.Code, b.Name, Product = b.ProducedItemName, ProductCode = b.ProducedItemCode, b.OutputQuantity, b.Uom, b.Version, Components = b.Components.Count, b.IsActive, Link = "/manufacturing/boms" }).ToListAsync(ct);
    }
}

internal static class ManufacturingAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Create a bill of materials", "Go to Manufacturing > Bills of materials and press Add BOM. Choose the product, add its parts with Add component, then press Save.", "/manufacturing/boms", Permissions.BomsRead),
        new("Create and release a work order", "Go to Manufacturing > Work orders, press New Work Order, choose the bill of materials and warehouse, and press Save work order. Press Release on its row to start production.", "/manufacturing/workorders", Permissions.WorkOrdersRead),
        new("Complete a work order", "On Manufacturing > Work orders press Complete on an order that is in progress and confirm with Complete. This receives the finished goods into stock and consumes the components.", "/manufacturing/workorders", Permissions.WorkOrdersRead),
        new("See what a product costs", "Go to Manufacturing > Product costing and choose a bill of materials to see the standard cost of a batch and how actual cost compares.", "/manufacturing/costing", Permissions.CostingRead),
    ];
}
