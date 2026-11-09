using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Domain.Customization;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Customization;

internal sealed class CustomFieldDefinitionRepository(IRepository<CustomFieldDefinition> definitions)
    : RepositoryBase<CustomFieldDefinition>(definitions), ICustomFieldDefinitionRepository
{
    public Task<List<CustomFieldDefinition>> ListForEntityTypeAsync(string entityType, bool includeInactive, CancellationToken ct)
        => Query(new Specification<CustomFieldDefinition>(d => d.EntityType == entityType && (includeInactive || d.IsActive)))
            .OrderBy(d => d.Order).ThenBy(d => d.Label).ToListAsync(ct);

    public Task<List<CustomFieldDefinition>> ListActiveForEntityTypeAsync(string entityType, CancellationToken ct)
        => Query(new Specification<CustomFieldDefinition>(d => d.EntityType == entityType && d.IsActive))
            .OrderBy(d => d.Order).ThenBy(d => d.Label).ToListAsync(ct);
}
