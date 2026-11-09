using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class LeadRepository(IRepository<Lead> inner) : RepositoryBase<Lead>(inner), ILeadRepository
{
    public Task<List<LeadStageRow>> ListStagesAsync(Guid? agentPartyId, CancellationToken ct)
        => Query(new Specification<Lead>(l => agentPartyId == null || l.AgentPartyId == agentPartyId)).Select(l => new LeadStageRow(l.Stage, l.FollowUpOn)).ToListAsync(ct);
}
