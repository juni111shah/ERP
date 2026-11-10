using ERP.Application.Abstractions;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Repositories;

internal sealed class SalesOrderRepository(IRepository<SalesOrder> inner) : RepositoryBase<SalesOrder>(inner), ISalesOrderRepository
{
    public Task<SalesOrder?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<SalesOrder>(x => x.Id == id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

    public Task<List<decimal>> ListOpenTotalsOfCustomerAsync(Guid customerPartyId, Guid legalEntityId, Guid exceptOrderId, CancellationToken ct)
        => Query(new Specification<SalesOrder>(o => o.CustomerPartyId == customerPartyId && o.LegalEntityId == legalEntityId && o.Id != exceptOrderId
                && (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing || o.Status == SalesOrderStatus.Delivered)))
            .Select(o => o.TotalAmount).ToListAsync(ct);

    public Task<int> CountOpenAsync(CancellationToken ct) => Open().CountAsync(ct);

    public async Task<decimal> TotalOpenAsync(CancellationToken ct) => await Open().SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

    public async Task<decimal> TotalNotCancelledAsync(CancellationToken ct)
        => await Query().Where(o => o.Status != SalesOrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

    private IQueryable<SalesOrder> Open() => Query().Where(o => o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing);
}
