using ERP.Domain.Common;

namespace ERP.Modules.Retail.Domain;

public enum PriceListType
{
    Standard = 0,
    Promotional = 1,
    Tiered = 2,
    Member = 3
}

public enum PromotionType
{
    PercentageDiscount = 0,
    FixedAmountDiscount = 1,
    BuyXGetY = 2,
    Bundle = 3
}

public class RetailStore : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    [Filterable]
    public Guid? WarehouseId { get; set; }

    [Searchable]
    public string? StoreManagerName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    [Filterable]
    public bool IsActive { get; set; } = true;

    public string? CustomFields { get; set; }
}

public class RetailPriceList : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    [Filterable]
    public PriceListType Type { get; set; } = PriceListType.Standard;

    public string Currency { get; set; } = "QAR";

    [Sortable, Filterable]
    public DateOnly? ValidFrom { get; set; }

    [Sortable, Filterable]
    public DateOnly? ValidTo { get; set; }

    [Filterable]
    public bool IsDefault { get; set; }

    [Filterable]
    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    public string? CustomFields { get; set; }

    public List<RetailPriceListItem> Items { get; set; } = new();
}

public class RetailPriceListItem : OrgEntity
{
    public Guid PriceListId { get; set; }

    public Guid? ItemId { get; set; }

    [Searchable]
    public string ItemCode { get; set; } = string.Empty;

    [Searchable]
    public string ItemName { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    public decimal Price { get; set; }

    public decimal? MinQuantity { get; set; }
}

public class RetailPromotion : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Code { get; set; } = string.Empty;

    [Searchable, Sortable]
    public string Name { get; set; } = string.Empty;

    [Filterable]
    public PromotionType Type { get; set; } = PromotionType.PercentageDiscount;

    public decimal DiscountValue { get; set; }

    public decimal? MinimumOrderAmount { get; set; }

    [Sortable, Filterable]
    public DateOnly StartDate { get; set; }

    [Sortable, Filterable]
    public DateOnly EndDate { get; set; }

    public int? UsageLimit { get; set; }

    public int TimesUsed { get; set; }

    [Filterable]
    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    public string? CustomFields { get; set; }
}
