using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosItemRepository(IRepository<Item> inner) : RepositoryBase<Item>(inner), IPosItemRepository
{
    public Task<Dictionary<Guid, Item>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).ToDictionaryAsync(i => i.Id, ct);

    public async Task<PosCatalogPage> PageActiveAsync(string? code, string? search, int page, int pageSize, CancellationToken ct)
    {
        var query = Query(new Specification<Item>(i => i.IsActive));
        if (!string.IsNullOrEmpty(code))
            query = query.Where(i => i.Barcode == code || i.Sku == code);
        else if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Name.Contains(search) || i.Sku.Contains(search) || (i.Barcode != null && i.Barcode.Contains(search)));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(i => i.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PosCatalogPage(rows, total);
    }
}
