using ERP.Application.Abstractions;
using ERP.Application.Assistant;
using ERP.Domain.Organizations;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

internal sealed class PurchaseOrdersAssistantReader(IReadRepository<PurchaseOrder> orders) : IAssistantReader
{
    public string Key => "purchase_orders";
    public string Description => "Purchase orders, newest first: number, vendor, dates, status and total.";
    public string? ModuleKey => PurchasingModule.Key;
    public string Permission => P.OrdersRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = orders.Query();
        if (search is not null) query = query.Where(o => o.Number.Contains(search) || o.VendorName.Contains(search));
        return await query.OrderByDescending(o => o.OrderDate).Take(take).Select(o => new { o.Number, Vendor = o.VendorName, o.OrderDate, o.ExpectedDeliveryDate, Status = o.Status.ToString(), o.Currency, o.TotalAmount, Link = "/purchasing/orders/" + o.Id }).ToListAsync(ct);
    }
}

internal sealed class RequisitionsAssistantReader(IReadRepository<PurchaseRequisition> requisitions, IReadRepository<LegalEntity> entities) : IAssistantReader
{
    public string Key => "purchase_requisitions";
    public string Description => "Purchase requisitions, newest first: number, requester, department, dates, status and estimated total.";
    public string? ModuleKey => PurchasingModule.Key;
    public string Permission => P.RequisitionsRead;

    public async Task<object> ListAsync(string? search, int take, CancellationToken ct)
    {
        var query = requisitions.Query();
        if (search is not null) query = query.Where(r => r.Number.Contains(search) || r.RequesterName.Contains(search) || (r.Department != null && r.Department.Contains(search)));
        return await query.OrderByDescending(r => r.RequisitionDate).Take(take).Select(r => new { r.Number, Requester = r.RequesterName, r.Department, r.RequisitionDate, r.RequiredDate, Status = r.Status.ToString(), Currency = entities.Query(null).Where(e => e.Id == r.LegalEntityId).Select(e => e.BaseCurrency).FirstOrDefault(), r.EstimatedTotal, Link = "/purchasing/requisitions" }).ToListAsync(ct);
    }
}

internal static class PurchasingAssistantHelp
{
    public static readonly IReadOnlyList<AssistantHelpArticle> Articles =
    [
        new("Create a purchase order", "Go to Purchases > Purchase orders and press New Purchase Order. Choose the vendor, add lines with Add Item, then press Create Purchase Order.", "/purchasing/orders", P.OrdersRead),
        new("Approve a purchase order and receive the goods", "Open the order from Purchases > Purchase orders. Press Submit, then Approve Order, and press Receive Goods (GRN) when the goods arrive. Generate Vendor Bill turns the order into a supplier bill.", "/purchasing/orders", P.OrdersRead),
        new("Raise a purchase requisition", "Go to Purchases > Requisitions, press New Requisition, add lines with Add Item and press Submit Requisition. It starts as a draft: press Submit on its row to send it for approval.", "/purchasing/requisitions", P.RequisitionsRead),
        new("Turn an approved requisition into a purchase order", "On Purchases > Requisitions an approved requisition shows Create PO on its row. Press it, choose the vendor and confirm with Create PO.", "/purchasing/requisitions", P.RequisitionsRead),
        new("Find goods receipts", "Purchases > Goods receipts lists every goods receipt note. New ones are recorded from the purchase order with Receive Goods (GRN).", "/purchasing/receipts", P.ReceiptsRead),
    ];
}
