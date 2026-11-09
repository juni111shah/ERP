using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Repositories;

internal sealed class StockCountRepository(IRepository<StockCount> inner) : RepositoryBase<StockCount>(inner), IStockCountRepository
{
    public async Task<StockCount> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(WithLines(id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<StockCount> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(WithLines(id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private static Specification<StockCount> WithLines(Guid id) => new Specification<StockCount>(c => c.Id == id).Include(c => c.Lines);
}
