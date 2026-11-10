using ERP.Application.Abstractions;

namespace ERP.Modules.Inventory.Repositories;

/// <summary>Data access for warehouses.</summary>
public interface IWarehouseRepository : IRepository<Warehouse>
{
    /// <summary>The name of each of the warehouses that exists, by id.</summary>
    Task<Dictionary<Guid, string>> MapNamesAsync(List<Guid> ids, CancellationToken ct);
}
