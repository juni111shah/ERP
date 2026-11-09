using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Repositories;

internal sealed class StatutoryRuleRepository(IRepository<StatutoryRule> inner) : RepositoryBase<StatutoryRule>(inner), IStatutoryRuleRepository
{
    public async Task<StatutoryRule> GetWithSlabsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<StatutoryRule>(r => r.Id == id).Include(r => r.Slabs)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<StatutoryRule> GetWithSlabsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<StatutoryRule>(r => r.Id == id).Include(r => r.Slabs)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
}
