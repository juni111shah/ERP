using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for owner payouts. Implemented in OwnerPayoutRepository.cs.</summary>
public interface IOwnerPayoutRepository : IRepository<OwnerPayout>
{
    /// <summary>The posted payout that raised the bill, tracked for changes; null when there is none.</summary>
    Task<OwnerPayout?> FindPostedForBillForUpdateAsync(Guid billId, CancellationToken ct);
}
