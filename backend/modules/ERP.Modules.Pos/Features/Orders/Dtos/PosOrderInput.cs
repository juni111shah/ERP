using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

public record PosOrderLineInput(
    Guid? ItemId,
    string? ItemCode = null,
    string? Description = null,
    decimal Quantity = 1,
    decimal? UnitPrice = null,
    decimal DiscountPercent = 0,
    decimal? TaxRate = null);

public record PosPaymentInput(
    PosPaymentMethod Method,
    decimal Amount,
    decimal TenderedAmount,
    decimal ChangeAmount,
    string? Reference);
