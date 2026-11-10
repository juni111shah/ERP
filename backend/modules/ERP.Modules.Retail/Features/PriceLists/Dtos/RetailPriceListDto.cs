using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

public record RetailPriceListItemDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string ItemName,
    decimal BasePrice,
    decimal Price,
    decimal? MinQuantity);

public record RetailPriceListDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    string? Description,
    string? CustomFields,
    List<RetailPriceListItemDto> Items,
    DateTimeOffset CreatedAt)
{
    public static RetailPriceListDto From(RetailPriceList p) => new(
        p.Id,
        p.LegalEntityId,
        p.Code,
        p.Name,
        p.Type,
        p.Currency,
        p.ValidFrom,
        p.ValidTo,
        p.IsDefault,
        p.IsActive,
        p.Description,
        p.CustomFields,
        p.Items.Select(i => new RetailPriceListItemDto(
            i.Id,
            i.ItemId,
            i.ItemCode,
            i.ItemName,
            i.BasePrice,
            i.Price,
            i.MinQuantity)).ToList(),
        p.CreatedAt);
}
