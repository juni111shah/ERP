using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>What customers owe at the end of a date; shared by the receivables ageing and the customer statement.</summary>
public sealed class ReceivablesAsOf(IInvoiceRepository invoices, ISettlementRepository settlements, ICreditApplicationRepository creditApplications)
{
    /// <summary>The amount still open on a document: positive for an invoice, negative for a credit note.</summary>
    public static decimal Signed(ReceivableOpenItem o) => (o.IsCreditNote ? -1 : 1) * (o.Total - o.Paid);

    /// <summary>Documents open at the end of <paramref name="asOf"/>: later receipts, credit applications and voids do not change the picture. An allocation counts from the date of its receipt.</summary>
    public async Task<List<ReceivableOpenItem>> OpenAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct, Guid? businessUnitId = null, string? sourceType = null, IReadOnlyCollection<Guid>? onlyParties = null)
    {
        var rows = await invoices.ListPostedAsOfAsync(asOf, entityId, partyId, businessUnitId, sourceType, onlyParties, ct);
        if (rows.Count == 0) return rows;
        var paid = await settlements.AllocatedPerInvoiceAsOfAsync(asOf, entityId, partyId, ct);
        var applied = await creditApplications.ListAsOfAsync(asOf, entityId, ct);
        foreach (var c in applied)
        {
            paid[c.CreditNoteId] = paid.GetValueOrDefault(c.CreditNoteId) + c.Amount; // used up on the credit note
            paid[c.InvoiceId] = paid.GetValueOrDefault(c.InvoiceId) + c.Amount;       // and settles the invoice
        }
        return rows.Select(o => o with { Paid = paid.GetValueOrDefault(o.Id) }).Where(o => o.Total - o.Paid > 0).ToList();
    }

    /// <summary>What is left unallocated on the receipts of each customer at the end of <paramref name="asOf"/>.</summary>
    public async Task<List<(Guid Party, string? Name, decimal Amount)>> UnallocatedAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var receipts = await settlements.ListNotVoidedAsOfAsync(InstrumentDirection.Received, asOf, entityId, partyId, ct);
        var allocated = await settlements.AllocatedPerSettlementAsOfAsync(InstrumentDirection.Received, asOf, entityId, partyId, ct);
        return receipts.Select(r => (r.PartyId, r.PartyName, Open: r.Amount - allocated.GetValueOrDefault(r.Id))).Where(r => r.Open > 0)
            .GroupBy(r => r.PartyId).Select(g => (g.Key, g.First().PartyName, g.Sum(r => r.Open))).ToList();
    }
}
