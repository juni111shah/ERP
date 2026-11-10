using ERP.Application.Abstractions;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Repositories;

/// <summary>Data access for retail price lists. Implemented in RetailPriceListRepository.cs.</summary>
public interface IRetailPriceListRepository : IRepository<RetailPriceList>
{
    /// <summary>Whether a price list with this code exists.</summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    /// <summary>The price list with its rows, read-only. Throws NotFoundException.</summary>
    Task<RetailPriceList> GetWithItemsAsync(Guid id, CancellationToken ct);

    /// <summary>The price list with its rows, tracked for changes. Throws NotFoundException.</summary>
    Task<RetailPriceList> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct);
}
