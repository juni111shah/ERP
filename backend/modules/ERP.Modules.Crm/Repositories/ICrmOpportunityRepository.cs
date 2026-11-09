using ERP.Application.Abstractions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Repositories;

/// <summary>Data access for CRM opportunities.</summary>
public interface ICrmOpportunityRepository : IRepository<CrmOpportunity>
{
    /// <summary>The opportunity, tracked for changes, or null when it does not exist.</summary>
    Task<CrmOpportunity?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Every opportunity of one legal entity or (null) all of them.</summary>
    Task<List<CrmOpportunity>> ListByLegalEntityAsync(Guid? legalEntityId, CancellationToken ct);
}
