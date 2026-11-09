using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>The tax codes purchasing bills with. Implemented in PurchasingTaxCodeRepository.cs.</summary>
public interface IPurchasingTaxCodeRepository : IRepository<TaxCode>
{
    /// <summary>The active tax code with this rate for the legal entity: recoverable first, then the default, then by code; null when there is none.</summary>
    Task<TaxCode?> FindBestForRateAsync(decimal rate, Guid legalEntityId, CancellationToken ct);
}
