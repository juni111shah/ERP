using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Pos.Repositories;

/// <summary>The tax codes as the till reads them. Implemented in PosTaxCodeRepository.cs.</summary>
public interface IPosTaxCodeRepository : IRepository<TaxCode>
{
    /// <summary>The tax codes with these ids, by id.</summary>
    Task<Dictionary<Guid, TaxCode>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The rate of the tax codes with these ids, by id.</summary>
    Task<Dictionary<Guid, decimal>> RatesAsync(List<Guid> ids, CancellationToken ct);
}
