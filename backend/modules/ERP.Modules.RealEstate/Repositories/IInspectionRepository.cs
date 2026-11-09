using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for inspections. Implemented in InspectionRepository.cs.</summary>
public interface IInspectionRepository : IRepository<Inspection>
{
    /// <summary>The inspection with its checklist, read-only. Throws NotFoundException.</summary>
    Task<Inspection> GetWithItemsAsync(Guid id, CancellationToken ct);

    /// <summary>The inspection with its checklist, tracked for changes. Throws NotFoundException.</summary>
    Task<Inspection> GetWithItemsForUpdateAsync(Guid id, CancellationToken ct);
}
