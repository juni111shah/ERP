using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

/// <summary>The lines and totals of a quote, shared by create and update.</summary>
internal static class QuoteLines
{
    public static void PopulateLinesAndTotals(SalesQuote quote, List<QuoteLineInput> lines)
    {
        quote.Lines.Clear();
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

            quote.Lines.Add(new SalesQuoteLine
            {
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

        quote.SubTotal = Math.Round(subtotal, 2);
        quote.DiscountTotal = Math.Round(discountTotal, 2);
        quote.TaxTotal = Math.Round(taxTotal, 2);
        quote.TotalAmount = Math.Round(subtotal + taxTotal, 2);
    }
}
