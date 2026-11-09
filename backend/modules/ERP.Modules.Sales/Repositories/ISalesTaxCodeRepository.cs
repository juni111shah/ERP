using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Sales.Repositories;

/// <summary>The tax codes sales invoices with. Implemented in SalesTaxCodeRepository.cs.</summary>
public interface ISalesTaxCodeRepository : IRepository<TaxCode>
{
    /// <summary>The active tax codes of the legal entity and the shared ones.</summary>
    Task<List<TaxCode>> ListActiveForLegalEntityAsync(Guid legalEntityId, CancellationToken ct);
}
