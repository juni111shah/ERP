using ERP.Application.Abstractions;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>Aggregates the period's tax per tax code, in the same way as the tax summary report, but keeps non-recoverable purchases (flagged) so a return can show all of them.</summary>
public sealed class TaxLineReader(IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<TaxCode> taxCodes)
{
    public async Task<IReadOnlyList<TaxLine>> ReadAsync(DateOnly from, DateOnly to, Guid entityId, CancellationToken ct)
    {
        // A document voided after its period was locked stays in that period (what was filed does not change) and comes back negative in the period of the void.
        var sales = await invoices.Query(new Specification<Invoice>(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.PendingApproval && (i.Status != DocumentStatus.Voided || i.VoidPostedOn != null) && i.LegalEntityId == entityId))
            .SelectMany(i => i.Lines, (i, l) => new { i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount }).ToListAsync(ct);
        var voidedSales = await invoices.Query(new Specification<Invoice>(i => i.Status == DocumentStatus.Voided && i.VoidPostedOn >= from && i.VoidPostedOn <= to && i.LegalEntityId == entityId))
            .SelectMany(i => i.Lines, (i, l) => new { IsCreditNote = !i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount }).ToListAsync(ct);
        sales.AddRange(voidedSales);
        var purchases = await bills.Query(new Specification<Bill>(b => b.BillDate >= from && b.BillDate <= to && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid || (b.Status == DocumentStatus.Voided && b.VoidPostedOn != null)) && b.LegalEntityId == entityId))
            .SelectMany(b => b.Lines, (b, l) => new { l.TaxCodeId, l.Net, l.TaxAmount, l.TaxRecoverable }).ToListAsync(ct);
        var voidedPurchases = await bills.Query(new Specification<Bill>(b => b.Status == DocumentStatus.Voided && b.VoidPostedOn >= from && b.VoidPostedOn <= to && b.LegalEntityId == entityId))
            .SelectMany(b => b.Lines, (b, l) => new { l.TaxCodeId, Net = -l.Net, TaxAmount = -l.TaxAmount, l.TaxRecoverable }).ToListAsync(ct);
        purchases.AddRange(voidedPurchases);
        var codes = await taxCodes.Query().Select(t => new { t.Id, t.Code, t.Kind, t.Rate, t.IsRecoverable }).ToDictionaryAsync(t => t.Id, ct);

        var result = new List<TaxLine>();
        foreach (var g in sales.GroupBy(s => s.TaxCodeId))
        {
            var c = g.Key is { } k && codes.TryGetValue(k, out var found) ? found : null;
            result.Add(new(TaxSide.Sales, c?.Code ?? "-", c?.Kind ?? TaxKind.Other, c?.Rate ?? 0, c?.IsRecoverable ?? true, g.Sum(x => x.IsCreditNote ? -x.Net : x.Net), g.Sum(x => x.IsCreditNote ? -x.TaxAmount : x.TaxAmount)));
        }
        foreach (var g in purchases.GroupBy(p => (p.TaxCodeId, p.TaxRecoverable)))
        {
            var c = g.Key.TaxCodeId is { } k && codes.TryGetValue(k, out var found) ? found : null;
            result.Add(new(TaxSide.Purchases, c?.Code ?? "-", c?.Kind ?? TaxKind.Other, c?.Rate ?? 0, g.Key.TaxRecoverable, g.Sum(x => x.Net), g.Sum(x => x.TaxAmount)));
        }
        return result;
    }
}
