using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class FundingInstalmentRepository(IRepository<FundingInstalment> inner) : RepositoryBase<FundingInstalment>(inner), IFundingInstalmentRepository
{
    public async Task<decimal> PaidPrincipalOfSourcesAsync(List<Guid> fundingSourceIds, CancellationToken ct)
        => (await Query(new Specification<FundingInstalment>(i => fundingSourceIds.Contains(i.FundingSourceId) && i.Status == InstalmentPayStatus.Paid)).Select(i => i.Principal).ToListAsync(ct)).Sum();

    public Task<List<FundingInstalment>> ListOfSourceAsync(Guid fundingSourceId, CancellationToken ct)
        => Query(new Specification<FundingInstalment>(i => i.FundingSourceId == fundingSourceId)).OrderBy(i => i.Sequence).ToListAsync(ct);
}
