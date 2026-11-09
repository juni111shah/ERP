using ERP.Application.Abstractions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Repositories;

/// <summary>Data access for CRM leads.</summary>
public interface ICrmLeadRepository : IRepository<CrmLead>
{
    /// <summary>The lead, tracked for changes, or null when it does not exist.</summary>
    Task<CrmLead?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Every lead of one legal entity or (null) all of them.</summary>
    Task<List<CrmLead>> ListByLegalEntityAsync(Guid? legalEntityId, CancellationToken ct);
}
