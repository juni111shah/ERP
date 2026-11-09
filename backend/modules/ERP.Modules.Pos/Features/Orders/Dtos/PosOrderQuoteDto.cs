namespace ERP.Modules.Pos.Features;

public record PosOrderQuoteLineDto(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal PromoDiscount,
    decimal TaxRate,
    decimal LineTotal);

public record PosOrderQuoteDto(
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? PromoCode,
    string? PromoName,
    decimal PromoDiscount,
    List<PosOrderQuoteLineDto> Lines);
