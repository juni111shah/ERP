using ERP.Application.Abstractions;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Repositories;

internal sealed class PosSessionRepository(IRepository<PosSession> inner) : RepositoryBase<PosSession>(inner), IPosSessionRepository
{
    public Task<PosSession?> FindOpenForRegisterAsync(Guid registerId, CancellationToken ct)
        => Query(new Specification<PosSession>(s => s.RegisterId == registerId && s.Status == PosSessionStatus.Open)).FirstOrDefaultAsync(ct);

    public Task<List<PosOpenSession>> ListOpenOfRegistersAsync(List<Guid> registerIds, CancellationToken ct)
        => Query(new Specification<PosSession>(s => registerIds.Contains(s.RegisterId) && s.Status == PosSessionStatus.Open))
            .Select(s => new PosOpenSession(s.RegisterId, s.Id)).ToListAsync(ct);

    public Task<PosSessionNotes?> FindNotesAsync(Guid id, CancellationToken ct)
        => Query(new Specification<PosSession>(s => s.Id == id)).Select(s => new PosSessionNotes(s.Notes)).FirstOrDefaultAsync(ct);

    public Task<PosSession> FirstAsync(Guid id, CancellationToken ct)
        => Query(new Specification<PosSession>(s => s.Id == id)).FirstAsync(ct);

    public Task<int> CloseIfOpenAsync(Guid id, Guid closedBy, DateTimeOffset closedAt, decimal actualCash, string? notes, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open))
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Status, PosSessionStatus.Closed)
                .SetProperty(s => s.ClosedByUserId, (Guid?)closedBy)
                .SetProperty(s => s.ClosedAt, (DateTimeOffset?)closedAt)
                .SetProperty(s => s.ActualCash, actualCash)
                .SetProperty(s => s.CashDifference, s => actualCash - s.ExpectedCash)
                .SetProperty(s => s.Notes, notes), ct);

    public Task<int> AddCashInAsync(Guid id, decimal amount, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open)).ExecuteUpdateAsync(set => set
            .SetProperty(s => s.TotalPayIn, s => s.TotalPayIn + amount)
            .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash + amount), ct);

    public Task<int> TakeCashOutAsync(Guid id, decimal amount, bool allowNegative, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open && (allowNegative || s.ExpectedCash >= amount))).ExecuteUpdateAsync(set => set
            .SetProperty(s => s.TotalPayOut, s => s.TotalPayOut + amount)
            .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - amount), ct);

    public Task<int> AddSaleAsync(Guid id, decimal total, decimal cashPaid, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open))
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.TotalSales, s => s.TotalSales + total)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash + cashPaid), ct);

    public Task<int> VoidSaleAsync(Guid id, decimal total, decimal cashRefunded, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open))
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.TotalReturns, s => s.TotalReturns + total)
                .SetProperty(s => s.TotalSales, s => s.TotalSales - total)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - cashRefunded), ct);

    public Task<int> AddReturnAsync(Guid id, decimal total, decimal cash, bool allowNegative, CancellationToken ct)
        => Tracked(new Specification<PosSession>(s => s.Id == id && s.Status == PosSessionStatus.Open && (allowNegative || s.ExpectedCash >= cash)))
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.TotalReturns, s => s.TotalReturns + total)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - cash), ct);

    public Task<List<decimal>> ListClosedCashDifferencesAsync(DateTime start, DateTime end, Guid? legalEntityId, Guid? registerId, CancellationToken ct)
        => Query(new Specification<PosSession>(s => s.ClosedAt != null && s.Status == PosSessionStatus.Closed
                && s.CreatedAt >= start && s.CreatedAt < end
                && (legalEntityId == null || s.LegalEntityId == legalEntityId) && (registerId == null || s.RegisterId == registerId)))
            .Select(s => s.CashDifference).ToListAsync(ct);
}
