using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>One taxed line of an issued invoice or credit note.</summary>
public sealed record SalesTaxLine(bool IsCreditNote, Guid? TaxCodeId, decimal Net, decimal TaxAmount);

/// <summary>One taxed line of a posted bill.</summary>
public sealed record PurchaseTaxLine(Guid? TaxCodeId, decimal Net, decimal TaxAmount, bool TaxRecoverable);

/// <summary>
/// The invoice and bill lines the tax summary report adds up. A document voided after its period was locked stays in that period and comes back
/// negative in the period of the void.
/// </summary>
public sealed class TaxSummaryLineReader(IReadRepository<Invoice> invoices, IReadRepository<Bill> bills)
{
    public async Task<List<SalesTaxLine>> ReadSalesAsync(DateOnly from, DateOnly to, Guid? entity, CancellationToken ct)
    {
        var sales = await invoices.Query(new Specification<Invoice>(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != DocumentStatus.Draft && (i.Status != DocumentStatus.Voided || i.VoidPostedOn != null) && (entity == null || i.LegalEntityId == entity)))
            .SelectMany(i => i.Lines, (i, l) => new SalesTaxLine(i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount)).ToListAsync(ct);
        sales.AddRange(await invoices.Query(new Specification<Invoice>(i => i.Status == DocumentStatus.Voided && i.VoidPostedOn >= from && i.VoidPostedOn <= to && (entity == null || i.LegalEntityId == entity)))
            .SelectMany(i => i.Lines, (i, l) => new SalesTaxLine(!i.IsCreditNote, l.TaxCodeId, l.Net, l.TaxAmount)).ToListAsync(ct));
        return sales;
    }

    public async Task<List<PurchaseTaxLine>> ReadPurchasesAsync(DateOnly from, DateOnly to, Guid? entity, CancellationToken ct)
    {
        var purchases = await bills.Query(new Specification<Bill>(b => b.BillDate >= from && b.BillDate <= to && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid || (b.Status == DocumentStatus.Voided && b.VoidPostedOn != null)) && (entity == null || b.LegalEntityId == entity)))
            .SelectMany(b => b.Lines, (b, l) => new PurchaseTaxLine(l.TaxCodeId, l.Net, l.TaxAmount, l.TaxRecoverable)).ToListAsync(ct);
        purchases.AddRange(await bills.Query(new Specification<Bill>(b => b.Status == DocumentStatus.Voided && b.VoidPostedOn >= from && b.VoidPostedOn <= to && (entity == null || b.LegalEntityId == entity)))
            .SelectMany(b => b.Lines, (b, l) => new PurchaseTaxLine(l.TaxCodeId, -l.Net, -l.TaxAmount, l.TaxRecoverable)).ToListAsync(ct));
        return purchases;
    }
}
