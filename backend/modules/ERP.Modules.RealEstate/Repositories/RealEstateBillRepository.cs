using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstateBillRepository(IRepository<Bill> inner) : RepositoryBase<Bill>(inner), IRealEstateBillRepository
{
    public Task<BillState?> FindStateAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.Id == id)).Select(b => new BillState(b.LegalEntityId, b.Status)).FirstOrDefaultAsync(ct);
}
