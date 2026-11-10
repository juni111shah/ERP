using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class DepositTransactionRepository(IRepository<DepositTransaction> inner) : RepositoryBase<DepositTransaction>(inner), IDepositTransactionRepository
{
    public Task<List<DepositTransaction>> ListOfLeaseAsync(Guid leaseId, CancellationToken ct)
        => Query(new Specification<DepositTransaction>(t => t.LeaseId == leaseId)).OrderBy(t => t.Date).ThenBy(t => t.CreatedAt).ToListAsync(ct);
}
