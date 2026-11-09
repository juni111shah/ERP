using System.Text.Json.Nodes;

namespace ERP.Modules.Inventory;

public sealed record WarehouseInput(string Code, string Name, Guid LegalEntityId, string? Address);
public sealed record WarehouseUpdateInput(string Name, string? Address, bool IsActive);
public sealed record WarehouseDto(Guid Id, string Code, string Name, Guid LegalEntityId, string? Address = null, bool IsActive = true);

/// <summary>A row of the warehouse list, with its custom field values.</summary>
public static class WarehouseRow
{
    public static object From(Warehouse w, JsonObject cf) => new { w.Id, w.Code, w.Name, w.LegalEntityId, w.Address, w.IsActive, customFields = cf };
}
