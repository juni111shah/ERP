using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class FundingSourceRepository(IRepository<FundingSource> inner) : RepositoryBase<FundingSource>(inner), IFundingSourceRepository
{
    public Task<List<FundingPrincipal>> ListPrincipalsOfAcquisitionsAsync(List<Guid> acquisitionIds, CancellationToken ct)
        => Query(new Specification<FundingSource>(f => acquisitionIds.Contains(f.AcquisitionId))).Select(f => new FundingPrincipal(f.Id, f.Principal)).ToListAsync(ct);

    public async Task<List<FundingSource>> ListAllByDueAsync(CancellationToken ct)
        => (await Query().ToListAsync(ct)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList();

    public Task<List<FundingSource>> ListOfAcquisitionForUpdateAsync(Guid acquisitionId, CancellationToken ct)
        => Tracked(new Specification<FundingSource>(f => f.AcquisitionId == acquisitionId)).ToListAsync(ct);

    public async Task<List<FundingSource>> ListOfAcquisitionByDueForUpdateAsync(Guid acquisitionId, CancellationToken ct)
        => (await Tracked(new Specification<FundingSource>(f => f.AcquisitionId == acquisitionId)).ToListAsync(ct)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList(); // ordered in memory: SQLite cannot sort decimals

    public async Task<List<FundingSource>> ListOfAcquisitionAsync(Guid acquisitionId, CancellationToken ct)
        => (await Query(new Specification<FundingSource>(f => f.AcquisitionId == acquisitionId)).ToListAsync(ct)).OrderBy(f => f.FirstDueDate).ThenByDescending(f => f.Principal).ToList();
}
