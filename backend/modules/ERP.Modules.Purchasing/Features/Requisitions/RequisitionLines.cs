using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Features;

/// <summary>The lines and estimated total of a requisition, shared by create and update.</summary>
internal static class RequisitionLines
{
    public static void PopulateLinesAndTotals(PurchaseRequisition req, List<RequisitionLineInput> lines)
    {
        req.Lines.Clear();
        decimal total = 0;

        foreach (var l in lines)
        {
            var lineTotal = Math.Round(l.Quantity * l.EstimatedUnitPrice, 2);
            total += lineTotal;

            req.Lines.Add(new PurchaseRequisitionLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim(),
                EstimatedUnitPrice = l.EstimatedUnitPrice,
                LineTotal = lineTotal
            });
        }

        req.EstimatedTotal = total;
    }
}
