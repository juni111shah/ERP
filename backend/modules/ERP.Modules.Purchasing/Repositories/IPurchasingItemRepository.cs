using ERP.Application.Abstractions;
using ERP.Domain.Catalog;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>The items as purchasing reads them. Implemented in PurchasingItemRepository.cs.</summary>
public interface IPurchasingItemRepository : IRepository<Item>
{
    /// <summary>Those of the items that are stocked products.</summary>
    Task<List<Guid>> ListProductIdsAsync(List<Guid> ids, CancellationToken ct);
}
