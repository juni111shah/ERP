using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Search;

/// <summary>Parties, items, invoices, bills, journals and cheques: the records every workspace has.</summary>
public sealed class CoreSearchProvider(
    IReadRepository<Party> parties, IReadRepository<Item> items, IReadRepository<Invoice> invoices, IReadRepository<Bill> bills, IReadRepository<JournalEntry> journals,
    IReadRepository<PaymentInstrument> instruments, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => null;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();
        if (user.HasPermission(CorePermissions.PartiesRead))
            hits.AddRange((await parties.Query(new Specification<Party>(p => p.Name.Contains(term) || p.Code.Contains(term) || (p.Email != null && p.Email.Contains(term)))).Take(limit).Select(p => new { p.Name, p.Code }).ToListAsync(ct))
                .Select(p => new SearchHit("Parties", p.Name, p.Code, $"/parties?search={Uri.EscapeDataString(p.Name)}")));
        if (user.HasPermission(CorePermissions.ItemsRead))
            hits.AddRange((await items.Query(new Specification<Item>(i => i.Sku.Contains(term) || i.Name.Contains(term))).Take(limit).Select(i => new { i.Name, i.Sku }).ToListAsync(ct))
                .Select(i => new SearchHit("Items", i.Name, i.Sku, "/catalog/items")));
        if (user.HasPermission(CorePermissions.InvoicesRead))
            hits.AddRange((await invoices.Query(new Specification<Invoice>(i => i.Number.Contains(term) || (i.PartyName != null && i.PartyName.Contains(term)))).Take(limit).Select(i => new { i.Number, i.PartyName }).ToListAsync(ct))
                .Select(i => new SearchHit("Invoices", i.Number, i.PartyName, $"/finance/invoices?search={Uri.EscapeDataString(i.Number)}")));
        if (user.HasPermission(CorePermissions.BillsRead))
            hits.AddRange((await bills.Query(new Specification<Bill>(b => b.Number.Contains(term) || (b.VendorReference != null && b.VendorReference.Contains(term)) || (b.PartyName != null && b.PartyName.Contains(term)))).Take(limit).Select(b => new { b.Number, b.PartyName }).ToListAsync(ct))
                .Select(b => new SearchHit("Bills", b.Number, b.PartyName, $"/finance/bills?search={Uri.EscapeDataString(b.Number)}")));
        if (user.HasPermission(CorePermissions.JournalsRead))
            hits.AddRange((await journals.Query(new Specification<JournalEntry>(j => j.Number.Contains(term) || (j.Memo != null && j.Memo.Contains(term)))).Take(limit).Select(j => new { j.Number, j.Memo }).ToListAsync(ct))
                .Select(j => new SearchHit("Journals", j.Number, j.Memo, $"/finance/journals?search={Uri.EscapeDataString(j.Number)}")));
        if (user.HasPermission(CorePermissions.ChequesRead))
            hits.AddRange((await instruments.Query(new Specification<PaymentInstrument>(i => i.InstrumentNo.Contains(term) || i.Number.Contains(term) || (i.PartyName != null && i.PartyName.Contains(term)))).Take(limit).Select(i => new { i.InstrumentNo, i.PartyName }).ToListAsync(ct))
                .Select(i => new SearchHit("Cheques", $"Cheque {i.InstrumentNo}", i.PartyName, $"/finance/cheques?search={Uri.EscapeDataString(i.InstrumentNo)}")));
        return hits;
    }
}
