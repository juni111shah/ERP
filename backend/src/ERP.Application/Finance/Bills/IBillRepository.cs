using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using ERP.Application.Portal;

namespace ERP.Application.Finance;

/// <summary>Data access for vendor bills. Implemented in ERP.Infrastructure/Repositories/Finance/BillRepository.cs.</summary>
public interface IBillRepository : IRepository<Bill>
{
    /// <summary>The bill with its lines, read-only. Throws NotFoundException.</summary>
    Task<Bill> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The bill with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<Bill> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The bills with these ids, read-only (missing ones are simply not returned).</summary>
    Task<List<Bill>> ListByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The bills with these ids, tracked for changes (missing ones are simply not returned).</summary>
    Task<List<Bill>> ListByIdsForUpdateAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The number of each of the bills, by id.</summary>
    Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The total of the vendor's statement bills dated before <paramref name="from"/>.</summary>
    Task<decimal> StatementTotalBeforeAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The vendor's statement bills dated from <paramref name="from"/> to <paramref name="to"/>: posted ones, and voided ones whose void came after the period was locked.</summary>
    Task<List<BillStatementRow>> ListStatementRowsAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The vendor's bills voided after their period was locked, the void posted on or before <paramref name="to"/>.</summary>
    Task<List<VoidedBillRow>> ListVoidedAfterLockAsync(Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>Bills dated on or before <paramref name="asOf"/> that were posted and not voided by the end of that day (<c>Paid</c> left at 0).</summary>
    Task<List<PayableOpenItem>> ListPostedAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct);

    /// <summary>Bill numbers by id, for the ids that exist.</summary>
    Task<Dictionary<Guid, string>> GetNumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The latest 200 bills of a portal vendor, leaving out drafts, ones pending approval and voided ones.</summary>
    Task<List<PortalBillDto>> ListForPortalAsync(Guid partyId, CancellationToken ct);

    /// <summary>The party's issued or partly paid bills dated on or before <paramref name="asOf"/>.</summary>
    Task<List<BillOpenItem>> ListOpenForPartyAsync(Guid partyId, DateOnly asOf, CancellationToken ct);

    /// <summary>Every issued or partly paid bill, of one legal entity or (null) all of them.</summary>
    Task<List<BillOpenBalance>> ListOpenBalancesAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>An open bill on a vendor's statement.</summary>
public sealed record BillOpenItem(string Number, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid);

/// <summary>What is still owed on one open bill.</summary>
public sealed record BillOpenBalance(string? PartyName, decimal Total, decimal Paid, DateOnly DueDate);
