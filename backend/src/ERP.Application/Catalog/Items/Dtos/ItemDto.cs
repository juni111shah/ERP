using System.Text.Json.Nodes;
using ERP.Domain.Catalog;

namespace ERP.Application.Catalog;

public sealed record ItemInput(
    string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId,
    bool IsActive = true, JsonObject? CustomFields = null, string? Barcode = null, string? Category = null);

public sealed record ItemDto(
    Guid Id, string Sku, string Name, ItemType Type, string Uom, decimal? SalesPrice, decimal? PurchasePrice, Guid? TaxCodeId,
    bool IsActive, JsonObject CustomFields, string? Barcode = null, string? Category = null)
{
    public static ItemDto From(Item i, JsonObject customFields) => new(i.Id, i.Sku, i.Name, i.Type, i.Uom, i.SalesPrice, i.PurchasePrice, i.TaxCodeId, i.IsActive, customFields, i.Barcode, i.Category);
}
