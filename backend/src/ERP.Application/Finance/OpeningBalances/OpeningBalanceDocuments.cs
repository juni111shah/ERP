using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

/// <summary>The parties an opening entry names, and the open invoices and bills it raised.</summary>
public sealed class OpeningBalanceDocuments(IRepository<Invoice> invoices, IRepository<Bill> bills, IReadRepository<Party> parties)
{
    /// <summary>The parties with the given ids, by id.</summary>
    public Task<Dictionary<Guid, Party>> MapPartiesAsync(List<Guid> partyIds, CancellationToken ct)
        => parties.Query(new Specification<Party>(p => partyIds.Contains(p.Id))).ToDictionaryAsync(p => p.Id, ct);

    /// <summary>The invoices raised by the opening entry, tracked for changes.</summary>
    public Task<List<Invoice>> ListInvoicesForUpdateAsync(Guid journalId, CancellationToken ct)
        => invoices.Tracked(new Specification<Invoice>(d => d.SourceType == LedgerSources.Opening && d.SourceId == journalId)).ToListAsync(ct);

    /// <summary>The bills raised by the opening entry, tracked for changes.</summary>
    public Task<List<Bill>> ListBillsForUpdateAsync(Guid journalId, CancellationToken ct)
        => bills.Tracked(new Specification<Bill>(d => d.SourceType == LedgerSources.Opening && d.SourceId == journalId)).ToListAsync(ct);
}
