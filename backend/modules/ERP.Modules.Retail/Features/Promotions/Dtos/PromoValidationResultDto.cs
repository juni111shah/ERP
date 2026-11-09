using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

public record PromoValidationResultDto(
    bool IsValid,
    string? Message,
    PromotionType? Type,
    decimal DiscountValue,
    decimal CalculatedDiscount);
