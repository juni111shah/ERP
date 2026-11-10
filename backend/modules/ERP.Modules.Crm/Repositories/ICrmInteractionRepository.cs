using ERP.Application.Abstractions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Repositories;

/// <summary>Data access for CRM interactions (activity logs).</summary>
public interface ICrmInteractionRepository : IRepository<CrmInteraction>
{
    /// <summary>The interaction, tracked for changes, or null when it does not exist.</summary>
    Task<CrmInteraction?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The interactions logged against one record, latest first.</summary>
    Task<List<CrmInteraction>> ListForRelatedEntityAsync(string relatedEntityKind, Guid relatedEntityId, CancellationToken ct);
}
