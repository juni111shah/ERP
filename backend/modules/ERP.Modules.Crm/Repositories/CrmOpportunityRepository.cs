using ERP.Application.Abstractions;
using ERP.Modules.Crm.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Repositories;

internal sealed class CrmOpportunityRepository(IRepository<CrmOpportunity> inner) : RepositoryBase<CrmOpportunity>(inner), ICrmOpportunityRepository
{
    public Task<CrmOpportunity?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<CrmOpportunity>> ListByLegalEntityAsync(Guid? legalEntityId, CancellationToken ct)
    {
        var query = Query();
        if (legalEntityId.HasValue)
        {
            query = query.Where(x => x.LegalEntityId == legalEntityId.Value);
        }
        return query.ToListAsync(ct);
    }
}
