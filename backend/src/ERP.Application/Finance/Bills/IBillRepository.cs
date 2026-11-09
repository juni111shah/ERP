using ERP.Application.Abstractions;
using ERP.Application.Portal;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for vendor bills. Implemented in ERP.Infrastructure/Repositories/Finance/BillRepository.cs.</summary>
public interface IBillRepository : IRepository<Bill>
{
    /// <summary>The bill with its lines, read-only. Throws NotFoundException.</summary>
    Task<Bill> GetWithLinesAsync(Guid id, CancellationToken ct);

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
