using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What the real estate features read from vendor bills. Implemented in RealEstateBillRepository.cs.</summary>
public interface IRealEstateBillRepository : IRepository<Bill>
{
    /// <summary>The legal entity and status of a bill; null when not found.</summary>
    Task<BillState?> FindStateAsync(Guid id, CancellationToken ct);
}

/// <summary>A bill's legal entity and status.</summary>
public sealed record BillState(Guid LegalEntityId, DocumentStatus Status);
