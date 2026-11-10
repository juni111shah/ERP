using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchaseReturnRepository(IRepository<PurchaseReturn> inner) : RepositoryBase<PurchaseReturn>(inner), IPurchaseReturnRepository
{
    public async Task<PurchaseReturn> GetWithLinesAndSharesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<PurchaseReturn>(r => r.Id == id)).Include(r => r.Lines).Include(r => r.BillShares).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase return '{id}' not found.");

    public Task<List<ReturnedBillLine>> ListPostedLinesOfBillAsync(Guid billId, Guid exceptReturnId, CancellationToken ct)
        => Query(new Specification<PurchaseReturn>(r => r.BillId == billId && r.Status == PurchaseReturnStatus.Posted && r.Id != exceptReturnId))
            .SelectMany(r => r.Lines).Select(l => new ReturnedBillLine(l.BillLineId, l.Quantity)).ToListAsync(ct);
}
