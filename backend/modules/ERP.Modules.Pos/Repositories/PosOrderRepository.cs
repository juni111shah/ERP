using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosOrderRepository(IRepository<PosOrder> inner) : RepositoryBase<PosOrder>(inner), IPosOrderRepository
{
    public Task<PosOrder?> FindWithLinesAndPaymentsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<PosOrder>(x => x.Id == id))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(ct);

    public Task<PosOrder?> FindByNumberWithLinesAndPaymentsAsync(string number, CancellationToken ct)
        => Query(new Specification<PosOrder>(x => x.Number == number))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(ct);

    public Task<PosOrder?> FindWithLinesAndPaymentsForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PosOrder>(x => x.Id == id))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(ct);

    public Task<PosOrder?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PosOrder>(o => o.Id == id)).FirstOrDefaultAsync(ct);

    public Task<PosOrder?> FindParkedForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PosOrder>(o => o.Id == id && o.Status == PosOrderStatus.Parked)).FirstOrDefaultAsync(ct);

    public Task<List<PosOrder>> ListParkedAsync(Guid? registerId, CancellationToken ct)
        => Query(new Specification<PosOrder>(o => o.Status == PosOrderStatus.Parked && (registerId == null || o.RegisterId == registerId)))
            .Include(o => o.Lines).Include(o => o.Payments).OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync(ct);

    public Task<List<PosOrder>> ListForSessionAsync(Guid sessionId, CancellationToken ct)
        => Query(new Specification<PosOrder>(o => o.SessionId == sessionId).Include(o => o.Lines).Include(o => o.Payments)).ToListAsync(ct);

    // CreatedAt carries the order date and compares in SQL on every provider.
    public Task<List<PosOrder>> ListInRangeAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct)
        => Query(new Specification<PosOrder>(o => o.CreatedAt >= start && o.CreatedAt < end
                && o.Status != PosOrderStatus.Parked
                && (legalEntityId == null || o.LegalEntityId == legalEntityId) && (registerId == null || o.RegisterId == registerId))
            .Include(o => o.Lines).Include(o => o.Payments)).ToListAsync(ct);

    public Task<int> CountCompletedSinceAsync(Guid? legalEntityId, DateTime startOfDayUtc, CancellationToken ct)
        => PosToday.CompletedOrders(Query(), legalEntityId, startOfDayUtc).CountAsync(ct);

    // summed as double so SQLite (tests) can aggregate in SQL too; rounded back to money
    public async Task<decimal> TotalCompletedSinceAsync(Guid? legalEntityId, DateTime startOfDayUtc, CancellationToken ct)
        => Math.Round((decimal)(await PosToday.CompletedOrders(Query(), legalEntityId, startOfDayUtc).SumAsync(o => (double?)o.TotalAmount, ct) ?? 0d), 2);
}
