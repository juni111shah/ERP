using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;

namespace ERP.Application.Catalog;

/// <summary>Rules shared by the item handlers.</summary>
internal static class ItemRules
{
    /// <summary>The custom-field entity type of items.</summary>
    public const string EntityType = "item";

    /// <summary>The trimmed barcode (null when empty), or a conflict when another item already has it.</summary>
    public static async Task<string?> CheckedBarcodeAsync(IReadRepository<Item> items, string? barcode, Guid itemId, CancellationToken ct)
    {
        var code = barcode?.Trim();
        if (string.IsNullOrEmpty(code)) return null;
        if (await items.AnyAsync(x => x.Barcode == code && x.Id != itemId, ct)) throw new ConflictException("Another item already has that barcode.");
        return code;
    }
}
