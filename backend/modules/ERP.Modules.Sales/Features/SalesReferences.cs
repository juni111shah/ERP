using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Domain.Parties;

namespace ERP.Modules.Sales.Features;

/// <summary>The customer and items a quote or order points at must exist in this workspace (the database cannot tell, its foreign keys ignore the workspace).</summary>
internal static class SalesReferences
{
    public static async Task EnsureAsync(IReferenceCheck references, Guid? customerPartyId, IEnumerable<Guid?> itemIds, CancellationToken ct)
    {
        if (customerPartyId is { } party && !await references.AllExistAsync<Party>([party], ct))
            throw new FieldValidationException("customerPartyId", "Customer not found.");
        if (!await references.AllExistAsync<Item>(itemIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "An item was not found.");
    }
}
