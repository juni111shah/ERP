using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosReturnRepository(IRepository<PosReturn> inner) : RepositoryBase<PosReturn>(inner), IPosReturnRepository
{
    public Task<PosReturn?> FindWithLinesAndRefundsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<PosReturn>(r => r.Id == id).Include(r => r.Lines).Include(r => r.Refunds)).FirstOrDefaultAsync(ct);

    public Task<List<PosRefundAmount>> ListRefundsOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<PosReturn>(r => r.OriginalOrderId == orderId)).SelectMany(r => r.Refunds).Select(f => new PosRefundAmount(f.Method, f.Amount)).ToListAsync(ct);

    public Task<List<PosReturnedLineAmount>> ListReturnedLinesOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<PosReturn>(r => r.OriginalOrderId == orderId)).SelectMany(r => r.Lines)
            .Select(l => new PosReturnedLineAmount(l.OrderLineId, l.NetAmount, l.TaxAmount)).ToListAsync(ct);

    public Task<List<PosReturn>> ListForSessionAsync(Guid sessionId, CancellationToken ct)
        => Query(new Specification<PosReturn>(r => r.SessionId == sessionId).Include(r => r.Refunds)).ToListAsync(ct);

    public Task<List<PosReturn>> ListInRangeAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct)
        => Query(new Specification<PosReturn>(r => r.CreatedAt >= start && r.CreatedAt < end
                && (legalEntityId == null || r.LegalEntityId == legalEntityId) && (registerId == null || r.RegisterId == registerId))
            .Include(r => r.Refunds)).ToListAsync(ct);

    // summed as double so SQLite (tests) can aggregate in SQL too; rounded back to money
    public async Task<decimal> TotalSinceAsync(Guid? legalEntityId, DateTime start, CancellationToken ct)
        => Math.Round((decimal)(await Query().Where(r => r.CreatedAt >= start && (legalEntityId == null || r.LegalEntityId == legalEntityId)).SumAsync(r => (double?)r.TotalAmount, ct) ?? 0d), 2);
}
