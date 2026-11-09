using ERP.Application.Abstractions;
using ERP.Domain.Customization;

namespace ERP.Application.Customization;

/// <summary>Data access for custom field definitions. Implemented in ERP.Infrastructure/Repositories/Customization/CustomFieldDefinitionRepository.cs.</summary>
public interface ICustomFieldDefinitionRepository : IRepository<CustomFieldDefinition>
{
    /// <summary>The definitions of <paramref name="entityType"/> in form order (order, then label); archived ones only when <paramref name="includeInactive"/>.</summary>
    Task<List<CustomFieldDefinition>> ListForEntityTypeAsync(string entityType, bool includeInactive, CancellationToken ct);

    /// <summary>The active definitions of <paramref name="entityType"/> in form order (order, then label).</summary>
    Task<List<CustomFieldDefinition>> ListActiveForEntityTypeAsync(string entityType, CancellationToken ct);
}
