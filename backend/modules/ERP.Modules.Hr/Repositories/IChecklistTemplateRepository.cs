using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for onboarding and offboarding checklist templates.</summary>
public interface IChecklistTemplateRepository : IRepository<ChecklistTemplate>
{
    /// <summary>The template with its steps, read-only. Throws NotFoundException.</summary>
    Task<ChecklistTemplate> GetWithItemsAsync(Guid id, CancellationToken ct);

    /// <summary>The template with its steps, tracked for changes. Throws NotFoundException.</summary>
    Task<ChecklistTemplate> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct);
}
