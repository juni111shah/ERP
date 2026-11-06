using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>Works out where an approved order stands from what has been received and billed.</summary>
internal static class PurchaseOrderProgress
{
    /// <summary>Billed only once everything ordered or received is billed and every stocked line has arrived, so goods billed ahead of receipt can still be received.</summary>
    public static async Task RefreshAsync(PurchaseOrder o, IReadRepository<Item> items, IStockPostingService stock, CancellationToken ct)
    {
        if (o.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Cancelled) return;
        var ids = o.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var stocked = await stock.IsInventoryEnabledAsync(ct) && ids.Count > 0
            ? (await items.Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).Select(i => i.Id).ToListAsync(ct)).ToHashSet()
            : [];
        var allBilled = o.Lines.Count > 0 && o.Lines.All(l => l.BilledQuantity >= Math.Max(l.Quantity, l.ReceivedQuantity)
            && (l.ItemId is not { } id || !stocked.Contains(id) || l.ReceivedQuantity >= l.Quantity));
        o.Status = allBilled ? PurchaseOrderStatus.Billed
            : o.Lines.Count > 0 && o.Lines.All(l => l.ReceivedQuantity >= l.Quantity) ? PurchaseOrderStatus.Received
            : o.Lines.Any(l => l.ReceivedQuantity > 0) ? PurchaseOrderStatus.PartiallyReceived
            : PurchaseOrderStatus.Approved;
    }
}

public static class PurchasingTransitions
{
    public static readonly StatusTransitions<PurchaseOrderStatus> Order = new(
        new Dictionary<PurchaseOrderStatus, IEnumerable<PurchaseOrderStatus>>
        {
            [PurchaseOrderStatus.Draft] = [PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Approved, PurchaseOrderStatus.Cancelled],
            [PurchaseOrderStatus.Submitted] = [PurchaseOrderStatus.Approved, PurchaseOrderStatus.Cancelled, PurchaseOrderStatus.Draft],
            [PurchaseOrderStatus.Approved] = [PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled, PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.PartiallyReceived] = [PurchaseOrderStatus.Received, PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.Received] = [PurchaseOrderStatus.Billed],
            [PurchaseOrderStatus.Billed] = [],
            [PurchaseOrderStatus.Cancelled] = [],
        },
        // Received and PartiallyReceived follow from goods receipts, Billed from bills: none of them can be set by hand.
        systemOnly: [PurchaseOrderStatus.Billed, PurchaseOrderStatus.Received, PurchaseOrderStatus.PartiallyReceived]);

    public static readonly StatusTransitions<PurchaseRequisitionStatus> Requisition = new(
        new Dictionary<PurchaseRequisitionStatus, IEnumerable<PurchaseRequisitionStatus>>
        {
            [PurchaseRequisitionStatus.Draft] = [PurchaseRequisitionStatus.PendingApproval, PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Rejected],
            [PurchaseRequisitionStatus.PendingApproval] = [PurchaseRequisitionStatus.Approved, PurchaseRequisitionStatus.Rejected],
            [PurchaseRequisitionStatus.Approved] = [PurchaseRequisitionStatus.Ordered],
            [PurchaseRequisitionStatus.Rejected] = [PurchaseRequisitionStatus.Draft],
            [PurchaseRequisitionStatus.Ordered] = [],
        },
        systemOnly: [PurchaseRequisitionStatus.Ordered]);
}
