using ERP.Application.Abstractions;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Repositories;

/// <summary>Data access for bills of materials. Implemented in BillOfMaterialsRepository.cs.</summary>
public interface IBillOfMaterialsRepository : IRepository<BillOfMaterials>
{
    /// <summary>The BOM with its components, read-only; null when it does not exist.</summary>
    Task<BillOfMaterials?> FindWithComponentsAsync(Guid id, CancellationToken ct);

    /// <summary>The BOM with its components, read-only. Throws NotFoundException.</summary>
    Task<BillOfMaterials> GetWithComponentsAsync(Guid id, CancellationToken ct);

    /// <summary>The BOM with its components, tracked for changes. Throws NotFoundException.</summary>
    Task<BillOfMaterials> GetWithComponentsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The legal entity's active BOMs other than <paramref name="excludeBomId"/>, with their components.</summary>
    Task<List<BillOfMaterials>> ListOtherActiveWithComponentsAsync(Guid legalEntityId, Guid? excludeBomId, CancellationToken ct);

    /// <summary>The legal entity's active BOMs plus the ones named in <paramref name="bomIds"/> (even if inactive), with their components.</summary>
    Task<List<BillOfMaterials>> ListActiveOrNamedWithComponentsAsync(Guid legalEntityId, List<Guid> bomIds, CancellationToken ct);

    /// <summary>An active BOM producing the item in the legal entity, with its components; null when there is none.</summary>
    Task<BillOfMaterials?> FindActiveForItemWithComponentsAsync(Guid legalEntityId, Guid itemId, CancellationToken ct);

    /// <summary>The number of active BOMs.</summary>
    Task<int> CountActiveAsync(CancellationToken ct);
}
