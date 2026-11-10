namespace ERP.Modules.Retail.Features;

public record RetailPriceListItemInput(
    Guid? ItemId,
    string ItemCode,
    string ItemName,
    decimal BasePrice,
    decimal Price,
    decimal? MinQuantity);
