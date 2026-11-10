using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Repositories;

internal sealed class RetailPriceListRepository(IRepository<RetailPriceList> inner) : RepositoryBase<RetailPriceList>(inner), IRetailPriceListRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct)
        => Query(new Specification<RetailPriceList>(x => x.Code == code))
            .AnyAsync(ct);

    public async Task<RetailPriceList> GetWithItemsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<RetailPriceList>(x => x.Id == id))
            .Include(x => x.Items)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"PriceList {id} not found");

    public async Task<RetailPriceList> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<RetailPriceList>(x => x.Id == id))
            .Include(x => x.Items)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"PriceList {id} not found");
}
