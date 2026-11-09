using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class OwnerPayoutRepository(IRepository<OwnerPayout> inner) : RepositoryBase<OwnerPayout>(inner), IOwnerPayoutRepository
{
    public Task<OwnerPayout?> FindPostedForBillForUpdateAsync(Guid billId, CancellationToken ct)
        => Tracked(new Specification<OwnerPayout>(x => x.BillId == billId && x.Status == OwnerPayoutStatus.Posted)).FirstOrDefaultAsync(ct);
}
