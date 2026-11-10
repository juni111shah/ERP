using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What rent billing reads from tax codes. Implemented in RealEstateTaxCodeRepository.cs.</summary>
public interface IRealEstateTaxCodeRepository : IRepository<TaxCode>
{
    /// <summary>The id of the active default tax code; null when there is none.</summary>
    Task<Guid?> FindActiveDefaultIdAsync(CancellationToken ct);
}
