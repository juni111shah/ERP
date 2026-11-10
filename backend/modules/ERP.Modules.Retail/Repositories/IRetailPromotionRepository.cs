using ERP.Application.Abstractions;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Repositories;

/// <summary>Data access for retail promotions. Implemented in RetailPromotionRepository.cs.</summary>
public interface IRetailPromotionRepository : IRepository<RetailPromotion>
{
    /// <summary>The legal entity's promotion with this code, read-only; null when there is none.</summary>
    Task<RetailPromotion?> FindByCodeAsync(string code, Guid legalEntityId, CancellationToken ct);

    /// <summary>How many times every promotion was used, together.</summary>
    Task<int> TotalTimesUsedAsync(CancellationToken ct);
}
