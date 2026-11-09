using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Parties;
using ERP.Domain.Catalog;
using ERP.Domain.Parties;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

/// <summary>
/// Rules shared by several purchase order commands: the header checks (create, update), the lines and totals (create, update)
/// and giving the requisition back when an order goes away (delete, cancel).
/// </summary>
public sealed class PurchaseOrderRules(IPurchaseRequisitionRepository requisitions, IReadRepository<Party> parties, IReferenceCheck references)
{
    /// <summary>The requisition behind a cancelled or deleted order still needs buying, so it goes back to approved and can be ordered again.</summary>
    public async Task ReleaseRequisitionAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (order.RequisitionId is not { } id) return;
        var requisition = await requisitions.FindConvertedToOrderForUpdateAsync(id, order.Id, ct);
        if (requisition is null) return;
        requisition.Status = PurchaseRequisitionStatus.Approved;
        requisition.ConvertedPurchaseOrderId = null;
    }

    /// <summary>Vendor, currency and name checks shared by create and update; returns the vendor name to store (the party's own name when a party is chosen).</summary>
    public async Task<string> ValidateHeaderAsync(PurchaseOrderInput i, string baseCurrency, CancellationToken ct)
    {
        if (!await references.AllExistAsync<Item>(i.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "An item was not found.");
        if (!string.Equals(i.Currency.Trim(), baseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new FieldValidationException("currency", $"The currency must be the legal entity's currency ({baseCurrency}).");
        if (i.VendorPartyId is not { } partyId) return i.VendorName.Trim();
        var party = await parties.FindAsync(partyId, ct) ?? throw new FieldValidationException("vendorPartyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        return party.Name;
    }

    /// <summary>Builds the order's lines from the input and sets its totals; the caller attaches the lines.</summary>
    public static List<PurchaseOrderLine> BuildLinesAndTotals(PurchaseOrder order, List<PurchaseOrderLineInput> lines)
    {
        var built = new List<PurchaseOrderLine>();
        decimal subtotal = 0;
        decimal taxTotal = 0;
        decimal discountTotal = 0;

        foreach (var l in lines)
        {
            var baseAmount = l.Quantity * l.UnitPrice;
            var disc = baseAmount * (l.DiscountPercent / 100m);
            var afterDisc = baseAmount - disc;
            var tax = afterDisc * (l.TaxRate / 100m);
            var lineTotal = afterDisc + tax;

            subtotal += afterDisc;
            discountTotal += disc;
            taxTotal += tax;

            built.Add(new PurchaseOrderLine
            {
                OrderId = order.Id,
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim(),
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = Math.Round(lineTotal, 2)
            });
        }

        order.SubTotal = Math.Round(subtotal, 2);
        order.DiscountTotal = Math.Round(discountTotal, 2);
        order.TaxTotal = Math.Round(taxTotal, 2);
        order.TotalAmount = Math.Round(subtotal + taxTotal, 2);
        return built;
    }
}
