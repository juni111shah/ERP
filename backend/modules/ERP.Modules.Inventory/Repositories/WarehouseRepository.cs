using ERP.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Repositories;

internal sealed class WarehouseRepository(IRepository<Warehouse> inner) : RepositoryBase<Warehouse>(inner), IWarehouseRepository
{
    public Task<Dictionary<Guid, string>> MapNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Warehouse>(w => ids.Contains(w.Id))).ToDictionaryAsync(w => w.Id, w => w.Name, ct);
}
