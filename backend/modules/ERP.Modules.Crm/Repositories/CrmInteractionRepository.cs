using ERP.Application.Abstractions;
using ERP.Modules.Crm.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Repositories;

internal sealed class CrmInteractionRepository(IRepository<CrmInteraction> inner) : RepositoryBase<CrmInteraction>(inner), ICrmInteractionRepository
{
    public Task<CrmInteraction?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<CrmInteraction>> ListForRelatedEntityAsync(string relatedEntityKind, Guid relatedEntityId, CancellationToken ct)
        => Query()
            .Where(x => x.RelatedEntityKind == relatedEntityKind && x.RelatedEntityId == relatedEntityId)
            .OrderByDescending(x => x.InteractionDate)
            .ToListAsync(ct);
}
