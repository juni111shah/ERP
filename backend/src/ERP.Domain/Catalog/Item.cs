using ERP.Domain.Common;

namespace ERP.Domain.Catalog;

public enum ItemType { Product, Service }

/// <summary>Products and services sold or bought by any business. Stock tracking belongs to the Inventory module.</summary>
[DefaultSort("sku")]
public class Item : OrgEntity, IHasCustomFields
{
    [Listable]
    public string Sku { get; set; } = default!;
    [Listable]
    public string Name { get; set; } = default!;
    /// <summary>What a scanner reads (EAN, UPC or the shop's own code). Optional and unique per workspace.</summary>
    [Searchable]
    public string? Barcode { get; set; }
    [Filterable, Sortable]
    public ItemType Type { get; set; }
    public string Uom { get; set; } = "ea";
    [Listable]
    public string? Category { get; set; }
    [Sortable, Filterable]
    public decimal? SalesPrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public Guid? TaxCodeId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    [Filterable]
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}
